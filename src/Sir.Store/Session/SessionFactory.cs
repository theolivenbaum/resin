using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Dispatcher of sessions.
    /// </summary>
    public class SessionFactory : IDisposable, ILogger
    {
        private readonly ITokenizer _tokenizer;
        private readonly IConfigurationProvider _config;
        private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, long>> _keys;
        private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<long, NodeReader>> _indexReaders;

        public string Dir { get; }

        public SessionFactory(string dir, ITokenizer tokenizer, IConfigurationProvider config)
        {
            Dir = dir;
            _keys = LoadKeys();
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

        public async Task<IList<(long offset, long length)>> ReadPageInfoFromDiskAsync(string ixpFileName)
        {
            using (var ixpStream = CreateReadStream(ixpFileName))
            {
                return await new PageIndexReader(ixpStream).ReadAllAsync();
            }
        }

        public long GetStreamLength(string ixpFileName)
        {
            var pages = ReadPageInfoFromDisk(ixpFileName);
            var last = pages[pages.Count - 1];
            var len = last.offset + last.length;

            return len;
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

                using (var stream = new FileStream(keyFile, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
                {
                    long i = 0;
                    var buf = new byte[sizeof(ulong)];
                    var read = stream.Read(buf, 0, buf.Length);

                    while (read > 0)
                    {
                        keys.GetOrAdd(BitConverter.ToUInt64(buf, 0), i++);

                        read = stream.Read(buf, 0, buf.Length);
                    }
                }
            }

            this.Log("loaded keys into memory in {0}", timer.Elapsed);

            return allkeys;
        }

        public void PersistKeyMapping(ulong collectionId, ulong keyHash, long keyId)
        {
            var fileName = Path.Combine(Dir, string.Format("{0}.kmap", collectionId));
            ConcurrentDictionary<ulong, long> keys;

            if (!_keys.TryGetValue(collectionId, out keys))
            {
                keys = new ConcurrentDictionary<ulong, long>();
                _keys.GetOrAdd(collectionId, keys);
            }

            if (!keys.ContainsKey(keyHash))
            {
                keys.GetOrAdd(keyHash, keyId);

                using (var stream = CreateAppendStream(fileName))
                {
                    stream.Write(BitConverter.GetBytes(keyHash), 0, sizeof(ulong));
                }
            }
        }

        public long GetKeyId(ulong collectionId, ulong keyHash)
        {
            return _keys[collectionId][keyHash];
        }

        public bool TryGetKeyId(ulong collectionId, ulong keyHash, out long keyId)
        {
            ConcurrentDictionary<ulong, long> keys;

            if (!_keys.TryGetValue(collectionId, out keys))
            {
                keys = new ConcurrentDictionary<ulong, long>();
                _keys.GetOrAdd(collectionId, keys);
            }

            if (!keys.TryGetValue(keyHash, out keyId))
            {
                keyId = -1;
                return false;
            }
            return true;
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

        public IndexSession CreateIndexSession(string collectionName, ulong collectionId)
        {
            var indexReaders = _indexReaders.GetOrAdd(collectionId, new ConcurrentDictionary<long, NodeReader>());

            return new IndexSession(collectionName, collectionId, this, _tokenizer, _config, indexReaders);
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