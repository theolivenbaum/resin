using Sir.RocksDb.Store;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace Sir.Store
{
    /// <summary>
    /// Dispatcher of sessions.
    /// </summary>
    public class SessionFactory : IDisposable, ILogger
    {
        private readonly ITokenizer _tokenizer;
        private readonly IConfigurationProvider _config;
        private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<long, NodeReader>> _indexReaders;

        public string Dir { get; }
        public IConfigurationProvider Config { get { return _config; } }

        public SessionFactory(string dir, ITokenizer tokenizer, IConfigurationProvider config)
        {
            Dir = dir;
            _tokenizer = tokenizer;
            _config = config;
            _indexReaders = new ConcurrentDictionary<ulong, ConcurrentDictionary<long, NodeReader>>();
        }

        public IList<(long offset, long length)> ReadPageInfoFromDisk(string ixpFileName)
        {
            using (var ixpStream = CreateReadStream(ixpFileName))
            {
                return new PageIndexReader(ixpStream).ReadAll();
            }
        }

        private ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, long>> LoadKeys()
        {
            var timer = new Stopwatch();
            timer.Start();

            var allkeys = new ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, long>>();

            foreach (var keyFile in Directory.GetFiles(Dir, "*.kmap"))
            {
                var collectionId = ulong.Parse(Path.GetFileNameWithoutExtension(keyFile));
                ConcurrentDictionary<ulong, long> keys;

                if (!allkeys.TryGetValue(collectionId, out keys))
                {
                    keys = new ConcurrentDictionary<ulong, long>();
                    allkeys.GetOrAdd(collectionId, keys);
                }

                using (var store = new RocksDbStore(keyFile))
                {
                    foreach (var kvp in store.GetAll())
                    {
                        keys.GetOrAdd(BitConverter.ToUInt64(kvp.Key), BitConverter.ToInt64(kvp.Value));
                    }
                }
            }

            this.Log("loaded keys into memory in {0}", timer.Elapsed);

            return allkeys;
        }

        public void PersistKeyMapping(ulong collectionId, ulong keyHash, long keyId)
        {
            var fileName = Path.Combine(Dir, string.Format("{0}.kmap", collectionId));

            using (var store = new RocksDbStore(fileName))
            {
                store.Put(BitConverter.GetBytes(keyHash), BitConverter.GetBytes(keyId));
            }
        }

        public long GetKeyId(ulong collectionId, ulong keyHash)
        {
            var fileName = Path.Combine(Dir, string.Format("{0}.kmap", collectionId));

            using (var store = new RocksDbStore(fileName))
            {
                var keyId = store.Get(BitConverter.GetBytes(keyHash));

                return BitConverter.ToInt64(keyId);
            }
        }

        public bool TryGetKeyId(ulong collectionId, ulong keyHash, out long keyId)
        {
            var dir = Path.Combine(Dir, string.Format("{0}.kmap", collectionId));
            var currentFn = Path.Combine(dir, "CURRENT");

            if (!File.Exists(currentFn))
            {
                keyId = -1;
                return false;
            }
            using (var store = new RocksDbStore(dir))
            {
                var keyIdBuf = store.Get(BitConverter.GetBytes(keyHash));

                if (keyIdBuf == null)
                {
                    keyId = -1;
                    return false;
                }

                keyId = BitConverter.ToInt64(keyIdBuf);
                return true;
            }
        }

        private readonly object _syncMMF = new object();

        public MemoryMappedFile OpenMMF(string fileName)
        {
            MemoryMappedFile mmf;
            var time = Stopwatch.StartNew();
            var mapName = fileName.Replace(":", "").Replace("\\", "_");

            try
            {
                mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read, HandleInheritability.Inheritable);

                this.Log($"opened existing mmf {mapName}");
            }
            catch (FileNotFoundException)
            {
                lock (_syncMMF)
                {
                    try
                    {
                        mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read, HandleInheritability.Inheritable);
                    }
                    catch (FileNotFoundException)
                    {
                        try
                        {
                            mmf = MemoryMappedFile.CreateFromFile(fileName, FileMode.Open, mapName, 0, MemoryMappedFileAccess.Read);
                            this.Log($"created new mmf {mapName}");

                        }
                        catch (IOException)
                        {
                            try
                            {
                                mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
                                this.Log($"opened existing mmf {mapName}");

                            }
                            catch (FileNotFoundException)
                            {
                                Thread.Sleep(100);
                                this.Log($"needed to pause thread to open mmf {mapName}");

                                mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
                                this.Log($"opened existing mmf {mapName}");
                            }
                        }
                    }
                }
            }

            this.Log($"mapping took {time.Elapsed}");

            return mmf;
        }

        public WarmupSession CreateWarmupSession(string collectionName, ulong collectionId, string baseUrl)
        {
            var indexReaders = _indexReaders.GetOrAdd(collectionId, new ConcurrentDictionary<long, NodeReader>());

            return new WarmupSession(collectionName, collectionId, this, _tokenizer, _config, indexReaders, baseUrl);
        }

        public OptimizeSession CreateOptimizeSession(string collectionName, ulong collectionId)
        {
            var indexReaders = _indexReaders.GetOrAdd(collectionId, new ConcurrentDictionary<long, NodeReader>());

            return new OptimizeSession(collectionName, collectionId, this, _config, indexReaders);
        }

        public DocumentStreamSession CreateDocumentStreamSession(string collectionName, ulong collectionId)
        {
            return new DocumentStreamSession(collectionName, collectionId, this);
        }

        public WriteSession CreateWriteSession(string collectionName, ulong collectionId)
        {
            return new WriteSession(collectionName, collectionId, this);
        }

        public TermIndexSession CreateIndexSession(string collectionName, ulong collectionId)
        {
            var indexReaders = _indexReaders.GetOrAdd(collectionId, new ConcurrentDictionary<long, NodeReader>());

            return new TermIndexSession(collectionName, collectionId, this, _tokenizer, _config, indexReaders);
        }

        public BowIndexSession CreateBOWSession(string collectionName, ulong collectionId)
        {
            var indexReaders = _indexReaders.GetOrAdd(collectionId, new ConcurrentDictionary<long, NodeReader>());

            return new BowIndexSession(collectionName, collectionId, this, _config, _tokenizer, indexReaders);
        }

        public ValidateSession CreateValidateSession(string collectionName, ulong collectionId)
        {
            var indexReaders = _indexReaders.GetOrAdd(collectionId, new ConcurrentDictionary<long, NodeReader>());

            return new ValidateSession(collectionName, collectionId, this, _tokenizer, _config, indexReaders);
        }

        public ReadSession CreateReadSession(string collectionName, ulong collectionId, string ixFileExtension = "ix",
            string ixpFileExtension = "ixp", string vecFileExtension = "vec")
        {
            var indexReaders = _indexReaders.GetOrAdd(collectionId, new ConcurrentDictionary<long, NodeReader>());

            return new ReadSession(collectionName, collectionId, this, _config, indexReaders, ixFileExtension, ixpFileExtension, vecFileExtension);
        }

        public Stream CreateAsyncReadStream(string fileName)
        {
            return File.Exists(fileName)
            ? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true)
            : null;
        }

        public Stream CreateReadStream(string fileName, FileOptions fileOptions = FileOptions.RandomAccess)
        {
            return File.Exists(fileName)
                ? new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, fileOptions)
                : null;
        }

        public Stream CreateAsyncAppendStream(string fileName)
        {
            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, true);
        }

        public Stream CreateAppendStream(string fileName)
        {
            // https://stackoverflow.com/questions/122362/how-to-empty-flush-windows-read-disk-cache-in-c
            //const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;
            //FileStream file = new FileStream(fileName, fileMode, fileAccess, fileShare, blockSize,
            //    FileFlagNoBuffering | FileOptions.WriteThrough | fileOptions);

            try
            {
                return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            }
            catch (IOException)
            {
                Thread.Sleep(100);

                return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            }
        }

        public bool CollectionExists(ulong collectionId)
        {
            return File.Exists(Path.Combine(Dir, collectionId + ".val"));
        }

        public void Dispose()
        {
        }
    }
}