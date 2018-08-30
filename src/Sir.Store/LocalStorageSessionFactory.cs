using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Sir.Store
{
    //TODO: extract interface
    public class LocalStorageSessionFactory
    {
        private readonly ITokenizer _tokenizer;
        private readonly SortedList<ulong, long> _keys;
        private VectorTree _index;
        private readonly StreamWriter _log;

        public Stream WritableKeyMapStream { get; }

        public string Dir { get; }

        public LocalStorageSessionFactory(string dir, ITokenizer tokenizer)
        {
            Dir = dir;
            _log = Logging.CreateLogWriter("localsessionfactory");
            _keys = LoadKeyMap();
            _tokenizer = tokenizer;
                
            LoadIndex();

            WritableKeyMapStream = new FileStream(
                Path.Combine(dir, "_.kmap"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            _log.Log("initialized");
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

        //private void RebuildIndexes()
        //{
        //    try
        //    {
        //        var timer = new Stopwatch();
        //        var batchTimer = new Stopwatch();

        //        timer.Start();

        //        var files = Directory.GetFiles(Dir, "*.docs");

        //        _log.Log(string.Format("re-indexing process found {0} document files", files.Length));

        //        foreach (var docFileName in files)
        //        {
        //            var name = Path.GetFileNameWithoutExtension(docFileName)
        //                .Split(".", StringSplitOptions.RemoveEmptyEntries);

        //            var collectionId = ulong.Parse(name[0]);

        //            using (var readSession = new DocumentReadSession(collectionId, this))
        //            {
        //                foreach (var batch in readSession.ReadDocs().Batch(1000))
        //                {
        //                    batchTimer.Restart();

        //                    using (var writeSession = new LocalStorageSessionFactory(Dir, new LatinTokenizer()).CreateWriteSession(collectionId))
        //                    {
        //                        var job = new IndexJob(collectionId, batch);

        //                        writeSession.WriteToIndex(job);
        //                    }
        //                    Console.WriteLine("wrote batch to index {0} in {1}", collectionId, batchTimer.Elapsed);
        //                }
        //            }
        //        }

        //        _log.Log(string.Format("rebuilt {0} indexes in {1}", files.Length, timer.Elapsed));
        //    }
        //    catch (Exception ex)
        //    {
        //        _log.Log(ex.ToString());
        //        throw;
        //    }

        //}

        public void LoadIndex()
        {
            var ix = new SortedList<ulong, SortedList<long, VectorNode>>();

            foreach (var ixFileName in Directory.GetFiles(Dir, "*.ix"))
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
            }

            _index = new VectorTree(ix);
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

        public WriteSession CreateWriteSession(ulong collectionId)
        {
            return new WriteSession(collectionId, this, _tokenizer);
        }

        public ReadSession CreateReadSession(ulong collectionId)
        {
            return new ReadSession(collectionId, this);
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
