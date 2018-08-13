using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    //TODO: extract interface
    public class LocalStorageSessionFactory : IDisposable
    {
        private readonly SortedList<ulong, long> _keys;
        private readonly object Sync = new object();
        private readonly VectorTree _index;

        public Stream ValueStream { get; }

        public Stream ValueIndexStream { get; }
        public Stream WritableValueStream { get; }

        public Stream WritableKeyMapStream { get; }

        public Stream WritableValueIndexStream { get; }

        public string Dir { get; }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;

            WritableValueStream.Dispose();
            ValueIndexStream.Dispose();
            WritableValueIndexStream.Dispose();
            WritableKeyMapStream.Dispose();
            ValueStream.Dispose();

            _disposed = true;
        }

        ~LocalStorageSessionFactory()
        {
            Dispose();
        }

        public LocalStorageSessionFactory(string dir)
        {
            _keys = LoadKeyMap(dir);
            _index = DeserializeIndexes(dir);
            Dir = dir;

            ValueStream = CreateReadWriteStream(Path.Combine(dir, "_.val"));
            WritableValueStream = CreateAppendStream(Path.Combine(dir, "_.val"));
            ValueIndexStream = CreateReadWriteStream(Path.Combine(dir, "_.vix"));
            WritableValueIndexStream = CreateAppendStream(Path.Combine(dir, "_.vix"));
            WritableKeyMapStream = new FileStream(Path.Combine(dir, "_.kmap"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }

        public void RefreshIndex(ulong collectionId, long keyId, VectorNode index)
        {
            _index.Replace(collectionId, keyId, index);
        }

        public static SortedList<ulong, long> LoadKeyMap(string dir)
        {
            var keys = new SortedList<ulong, long>();

            using (var stream = new FileStream(Path.Combine(dir, "_.kmap"), FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
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

        public VectorTree DeserializeIndexes(string dir)
        {
            var ix = new SortedList<ulong, SortedList<long, VectorNode>>();

            foreach (var ixFileName in Directory.GetFiles(dir, "*.ix"))
            {
                var name = Path.GetFileNameWithoutExtension(ixFileName).Split(".", StringSplitOptions.RemoveEmptyEntries);
                var collectionHash = ulong.Parse(name[0]);
                var keyId = long.Parse(name[1]);
                SortedList<long, VectorNode> colIx;
                var vecFileName = Path.Combine(dir, string.Format("{0}.vec", collectionHash));

                if (!ix.TryGetValue(collectionHash, out colIx))
                {
                    colIx = new SortedList<long, VectorNode>();
                    ix.Add(collectionHash, colIx);
                }

                var root = DeserializeIndex(ixFileName, vecFileName);
                ix[collectionHash].Add(keyId, root);
            }

            return new VectorTree(ix);
        }

        public VectorNode DeserializeIndex(string ixFileName, string vecFileName)
        {
            using (var treeStream = new FileStream(ixFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var vecStream = new FileStream(vecFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return VectorNode.Deserialize(treeStream, vecStream);
            }
        }

        public void AddKey(ulong keyHash, long keyId)
        {
            _keys.Add(keyHash, keyId);

            var buf = BitConverter.GetBytes(keyHash);

            WritableKeyMapStream.Write(buf, 0, sizeof(ulong));
            WritableKeyMapStream.Flush();
        }

        public long GetKey(ulong keyHash)
        {
            return _keys[keyHash];
        }

        public bool TryGetKeyId(ulong keyHash, out long keyId)
        {
            if (!_keys.TryGetValue(keyHash, out keyId))
            {
                keyId = 0;
                return false;
            }
            return true;
        }

        public WriteSession CreateWriteSession(ulong collectionId)
        {
            return new WriteSession(collectionId, this);
        }

        public ReadSession CreateReadSession(ulong collectionId)
        {
            return new ReadSession(collectionId, this);
        }

        public SortedList<long, VectorNode> GetIndex(ulong collectionId)
        {
            return _index.GetOrCreateIndex(collectionId);
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
