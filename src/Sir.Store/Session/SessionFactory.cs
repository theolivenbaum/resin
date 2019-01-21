using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Dispatcher of sessions.
    /// </summary>
    public class SessionFactory : IDisposable, ILogger
    {
        private readonly ITokenizer _tokenizer;
        private readonly IConfigurationProvider _config;
        private readonly ConcurrentDictionary<ulong, long> _keys;

        private Stream _writableKeyMapStream { get; }

        public string Dir { get; }

        public SessionFactory(string dir, ITokenizer tokenizer, IConfigurationProvider config)
        {
            Dir = dir;
            _keys = LoadKeyMap();
            _tokenizer = tokenizer;
            _config = config;
            _writableKeyMapStream = CreateAppendStream(Path.Combine(dir, "_.kmap"));
        }

        private ConcurrentDictionary<ulong, long> LoadKeyMap()
        {
            var timer = new Stopwatch();
            timer.Start();

            var keys = new ConcurrentDictionary<ulong, long>();

            using (var stream = new FileStream(
                Path.Combine(Dir, "_.kmap"), FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
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

            this.Log("loaded keys into memory in {0}", timer.Elapsed);

            return keys;
        }

        private void ValidateIndex()
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                this.Log("begin validating");

                var indexFiles = Directory.GetFiles(Dir, "*.ix");

                foreach (var ixFileName in indexFiles)
                {
                    var name = Path.GetFileNameWithoutExtension(ixFileName)
                        .Split(".", StringSplitOptions.RemoveEmptyEntries);

                    var collectionHash = ulong.Parse(name[0]);
                    var keyId = long.Parse(name[1]);
                    var vecFileName = Path.Combine(Dir, string.Format("{0}.vec", collectionHash));
                    var pageIndexFileName = Path.Combine(Dir, string.Format("{0}.{1}.ixp", collectionHash, keyId));

                    using (var ixpStream = CreateAsyncReadStream(pageIndexFileName))
                    {
                        var nodeReader = new NodeReader(ixFileName, vecFileName, ixpStream, this);

                        // validate
                        foreach (var validateFn in Directory.GetFiles(Dir, string.Format("*.validate")))
                        {
                            this.Log("validating {0}", validateFn);

                            var fi = new FileInfo(validateFn);
                            var segs = Path.GetFileNameWithoutExtension(fi.Name).Split('.');
                            var col = ulong.Parse(segs[0]);
                            var key = long.Parse(segs[1]);

                            if (col == collectionHash && key == keyId)
                            {
                                string[] lines = File.ReadAllLines(validateFn);

                                if (lines != null)
                                {
                                    foreach (var token in File.ReadAllLines(validateFn))
                                    {
                                        var closestMatch = nodeReader.ClosestMatch(new VectorNode(token).Vector).FirstOrDefault();

                                        if (closestMatch != null && closestMatch.Score < VectorNode.IdenticalAngle)
                                        {
                                            throw new DataMisalignedException();
                                        }
                                        else
                                        {
                                            File.Delete(validateFn);
                                        }
                                    }
                                }
                            }                           
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.Log(ex);

                throw;
            }
        }

        public void PersistKeyMapping(ulong keyHash, long keyId)
        {
            if (!_keys.ContainsKey(keyHash))
            {
                _keys.GetOrAdd(keyHash, keyId);

                _writableKeyMapStream.Write(BitConverter.GetBytes(keyHash), 0, sizeof(ulong));

                _writableKeyMapStream.Flush();
            }
        }

        public long GetKeyId(ulong keyHash)
        {
            return _keys[keyHash];
        }

        public bool TryGetKeyId(ulong keyHash, out long keyId)
        {
            if (!_keys.TryGetValue(keyHash, out keyId))
            {
                keyId = -1;
                return false;
            }
            return true;
        }

        public MemoryMappedFile CreateMMF(string fileName, string mapName)
        {
            try
            {
                return MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
            }
            catch
            {
                try
                {
                    return MemoryMappedFile.CreateFromFile(fileName, FileMode.Open, mapName, 0, MemoryMappedFileAccess.Read);
                }
                catch
                {
                    return MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
                }
            }
        }

        public DocumentStreamSession CreateDocumenSession(string collectionId)
        {
            return new DocumentStreamSession(collectionId, this);
        }

        public WriteSession CreateWriteSession(string collectionId)
        {
            return new WriteSession(collectionId, this);
        }

        public IndexingSession CreateIndexSession(string collectionId)
        {
            return new IndexingSession(collectionId, this, _tokenizer, _config);
        }

        public ReadSession CreateReadSession(string collectionId)
        {
            return new ReadSession(collectionId, this, _config);
        }

        public Stream CreateAsyncReadStream(string fileName)
        {
            return new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
        }

        public Stream CreateReadStream(string fileName)
        {
            return new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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

            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }

        public void Dispose()
        {
            _writableKeyMapStream.Dispose();
        }
    }
}