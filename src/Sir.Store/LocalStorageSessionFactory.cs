using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Sir.Store
{
    public class LocalStorageSessionFactory
    {
        private readonly ITokenizer _tokenizer;
        private readonly IConfigurationService _config;
        private readonly SortedList<ulong, long> _keys;
        private VectorTree _index;
        private readonly StreamWriter _log;

        public Stream WritableKeyMapStream { get; }

        public string Dir { get; }

        public LocalStorageSessionFactory(string dir, ITokenizer tokenizer, IConfigurationService config)
        {
            Dir = dir;
            _log = Logging.CreateWriter("sessionfactory");
            _keys = LoadKeyMap();
            _tokenizer = tokenizer;
            _config = config;

            LoadIndex();

            WritableKeyMapStream = new FileStream(
                Path.Combine(dir, "_.kmap"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }

        public void AddIndex(ulong collectionId, long keyId, VectorNode index)
        {
            _index.Add(collectionId, keyId, index);
        }

        private SortedList<ulong, long> LoadKeyMap()
        {
            var keys = new SortedList<ulong, long>();

            using (var stream = new FileStream(
                Path.Combine(Dir, "_.kmap"), FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
            {
                long i = 0;
                var buf = new byte[sizeof(ulong)];
                var read = stream.Read(buf, 0, buf.Length);

                while (read > 0)
                {
                    keys.Add(BitConverter.ToUInt64(buf, 0), i++);

                    read = stream.Read(buf, 0, buf.Length);
                }
            }
            return keys;
        }

        public void LoadIndex()
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                _log.Log("begin loading index into memory");

                var ix = new SortedList<ulong, SortedList<long, VectorNode>>();
                var indexFiles = Directory.GetFiles(Dir, "*.ix");

                foreach (var ixFileName in indexFiles)
                {
                    var name = Path.GetFileNameWithoutExtension(ixFileName)
                        .Split(".", StringSplitOptions.RemoveEmptyEntries);

                    var collectionHash = ulong.Parse(name[0]);
                    var keyId = long.Parse(name[1]);
                    var vecFileName = Path.Combine(Dir, string.Format("{0}.vec", collectionHash));

                    SortedList<long, VectorNode> colIx;

                    if (!ix.TryGetValue(collectionHash, out colIx))
                    {
                        colIx = new SortedList<long, VectorNode>();
                        ix.Add(collectionHash, colIx);
                    }

                    var root = DeserializeIndex(ixFileName, vecFileName);
                    ix[collectionHash].Add(keyId, root);

                    _log.Log(string.Format("loaded {0}.{1}. {2}",
                        collectionHash, keyId, root.Size()));
                }

                _index = new VectorTree(ix);

                if (indexFiles.Length == 0)
                {
                    _log.Log("found no index files in {0}. index is empty.", Dir);
                }
                else
                {
                    _log.Log("deserialized {0} index files in {1}", indexFiles.Length, timer.Elapsed);
                }
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        public VectorNode DeserializeIndex(string ixFileName, string vecFileName)
        {
            using (var treeStream = new FileStream(ixFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var vecStream = new FileStream(vecFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return VectorNode.Deserialize(treeStream, vecStream);
            }
        }

        public void PersistKeyMapping(ulong keyHash, long keyId)
        {
            _keys.Add(keyHash, keyId);

            var buf = BitConverter.GetBytes(keyHash);

            WritableKeyMapStream.Write(buf, 0, sizeof(ulong));
            WritableKeyMapStream.Flush();
        }

        public long GetKeyId(ulong keyHash)
        {
            return _keys[keyHash];
        }

        public bool TryGetIndex(ulong collectionId, long keyId, out VectorNode index)
        {
            var colIndex = _index.GetIndex(collectionId);

            if (colIndex != null)
            {
                return colIndex.TryGetValue(keyId, out index);
            }

            index = null;

            return false;
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

        public SortedList<long, VectorNode> GetCollectionIndex(ulong collectionId)
        {
            return _index.GetIndex(collectionId) ?? new SortedList<long, VectorNode>();
        }

        public WriteSession CreateWriteSession(string collectionId)
        {
            return new WriteSession(collectionId, this, _tokenizer);
        }

        public IndexingSession CreateIndexSession(string collectionId)
        {
            return new IndexingSession(collectionId, this, _tokenizer, _config);
        }

        public ReadSession CreateReadSession(string collectionId)
        {
            return new ReadSession(collectionId, this, _config);
        }

        public Stream CreateWriteStream(string fileName)
        {
            return new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        }

        public Stream CreateReadWriteStream(string fileName)
        {
            return new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        public Stream CreateAppendStream(string fileName)
        {
            // https://stackoverflow.com/questions/122362/how-to-empty-flush-windows-read-disk-cache-in-c
            //const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;
            //FileStream file = new FileStream(fileName, fileMode, fileAccess, fileShare, blockSize,
            //    FileFlagNoBuffering | FileOptions.WriteThrough | fileOptions);

            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }
    }
}
