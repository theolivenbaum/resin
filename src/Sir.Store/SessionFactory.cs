using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public class SessionFactory : IDisposable
    {
        private readonly SortedList<ulong, long> _keys;
        private readonly object Sync = new object();
        private readonly VectorTree _index;
        private readonly string _dir;

        public Stream ValueStream { get; }

        public Stream ValueIndexStream { get; }
        public Stream WritableValueStream { get; }

        public Stream WritableKeyMapStream { get; }

        public Stream WritableValueIndexStream { get; }

        public void Dispose()
        {
            WritableValueStream.Dispose();
            ValueIndexStream.Dispose();
            WritableValueIndexStream.Dispose();
            WritableKeyMapStream.Dispose();
            ValueStream.Dispose();
        }

        public SessionFactory(string dir)
        {
            _keys = LoadKeyMap(dir);
            _index = DeserializeTree(dir);
            _dir = dir;

            ValueStream = CreateReadWriteStream(Path.Combine(dir, "_.val"));
            WritableValueStream = CreateAppendStream(Path.Combine(dir, "_.val"));
            ValueIndexStream = CreateReadWriteStream(Path.Combine(dir, "_.vix"));
            WritableValueIndexStream = CreateAppendStream(Path.Combine(dir, "_.vix"));
            WritableKeyMapStream = new FileStream(Path.Combine(dir, "_.kmap"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
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

        public static VectorTree DeserializeTree(string dir)
        {
            var ix = new SortedList<ulong, SortedList<long, VectorNode>>();

            foreach (var ixFileName in Directory.GetFiles(dir, "*.ix"))
            {
                var name = Path.GetFileNameWithoutExtension(ixFileName).Split(".", StringSplitOptions.RemoveEmptyEntries);
                var colHash = ulong.Parse(name[0]);
                var keyId = long.Parse(name[1]);
                SortedList<long, VectorNode> colIx;

                if (!ix.TryGetValue(colHash, out colIx))
                {
                    colIx = new SortedList<long, VectorNode>();
                    ix.Add(colHash, colIx);
                }

                using (var treeStream = File.OpenRead(ixFileName))
                using (var vecStream = File.OpenRead(Path.Combine(dir, string.Format("{0}.vec", colHash))))
                {
                    var root = VectorNode.Deserialize(treeStream, vecStream);

                    ix[colHash].Add(keyId, root);
                }
            }

            return new VectorTree(ix);
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

        public bool TryGetKeyId(ulong key, out long keyId)
        {
            if (!_keys.TryGetValue(key, out keyId))
            {
                keyId = 0;
                return false;
            }
            return true;
        }

        public WriteSession CreateWriteSession(ulong collectionId)
        {
            return new WriteSession(_dir, collectionId, this);
        }

        public ReadSession CreateReadSession(ulong collectionId)
        {
            return new ReadSession(_dir, collectionId, this);
        }

        public SortedList<long, VectorNode> GetIndex(ulong collectionId)
        {
            return _index.GetOrCreateIndex(collectionId);
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

        public Stream CreateReadStream(string fileName)
        {
            if (File.Exists(fileName))
            {
                return new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            return null;
        }
    }
}
