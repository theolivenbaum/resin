using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sir.Store
{
    public class SessionFactory : IDisposable
    {
        private readonly SortedList<ulong, uint> _keys;
        private readonly object Sync = new object();
        private readonly VectorTree _index;
        private readonly string _dir;
        private readonly Stream _writableValueStream;
        private readonly Stream _writableKeyMapStream;
        private readonly Stream _writableValueIndexStream;
        private readonly Stream _valueStream;
        private readonly Stream _valueIndexStream;

        public void Dispose()
        {
            _writableValueStream.Dispose();
            _valueIndexStream.Dispose();
            _writableValueIndexStream.Dispose();
            _writableKeyMapStream.Dispose();
            _valueStream.Dispose();
        }

        public SessionFactory(string dir)
        {
            _keys = LoadKeyMap(dir);
            _index = DeserializeTree(dir);
            _dir = dir;
            _valueStream = CreateReadWriteStream(Path.Combine(dir, "_.val"));
            _writableValueStream = CreateAppendStream(Path.Combine(dir, "_.val"));
            _valueIndexStream = CreateReadWriteStream(Path.Combine(dir, "_.vix"));
            _writableValueIndexStream = CreateAppendStream(Path.Combine(dir, "_.vix"));
            _writableKeyMapStream = new FileStream(Path.Combine(dir, "_.kmap"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }

        private static SortedList<ulong, uint> LoadKeyMap(string dir)
        {
            var keys = new SortedList<ulong, uint>();

            using (var stream = new FileStream(Path.Combine(dir, "_.kmap"), FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
            {
                uint i = 0;
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

        private static VectorTree DeserializeTree(string dir)
        {
            var ix = new SortedList<ulong, SortedList<uint, VectorNode>>();

            foreach (var ixFileName in Directory.GetFiles(dir, "*.ix"))
            {
                var name = Path.GetFileNameWithoutExtension(ixFileName).Split(".", StringSplitOptions.RemoveEmptyEntries);
                var colHash = ulong.Parse(name[0]);
                var keyId = uint.Parse(name[1]);
                SortedList<uint, VectorNode> colIx;

                if (!ix.TryGetValue(colHash, out colIx))
                {
                    colIx = new SortedList<uint, VectorNode>();
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

        public void AddKey(ulong keyHash, uint keyId)
        {
            _keys.Add(keyHash, keyId);

            var buf = BitConverter.GetBytes(keyHash);

            _writableKeyMapStream.Write(buf, 0, sizeof(ulong));
            _writableKeyMapStream.Flush();
        }

        public uint GetKey(ulong keyHash)
        {
            return _keys[keyHash];
        }

        public bool TryGetKeyId(ulong key, out uint keyId)
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
            return new WriteSession(_dir, collectionId, this)
            {
                ValueStream = _writableValueStream,
                KeyStream = CreateAppendStream(string.Format("{0}.key", collectionId)),
                DocStream = CreateAppendStream(string.Format("{0}.docs", collectionId)),
                ValueIndexStream = _writableValueIndexStream,
                KeyIndexStream = CreateAppendStream(string.Format("{0}.kix", collectionId)),
                DocIndexStream = CreateAppendStream(string.Format("{0}.dix", collectionId)),
                PostingsStream = CreateReadWriteStream(string.Format("{0}.pos", collectionId)),
                VectorStream = CreateAppendStream(string.Format("{0}.vec", collectionId)),
                Index = GetIndex(collectionId)
            };
        }

        public ReadSession CreateReadSession(ulong collectionId)
        {
            var sess = new ReadSession(_dir, collectionId, this)
            {
                ValueStream = _valueStream,
                KeyStream = CreateReadStream(string.Format("{0}.key", collectionId)),
                DocStream = CreateReadStream(string.Format("{0}.docs", collectionId)),
                ValueIndexStream = _valueIndexStream,
                KeyIndexStream = CreateReadStream(string.Format("{0}.kix", collectionId)),
                DocIndexStream = CreateReadStream(string.Format("{0}.dix", collectionId)),
                PostingsStream = CreateReadStream(string.Format("{0}.pos", collectionId)),
                VectorStream = CreateReadStream(string.Format("{0}.vec", collectionId)),
                Index = GetIndex(collectionId)
            };

            return sess;
        }

        protected SortedList<uint, VectorNode> GetIndex(ulong collectionId)
        {
            return _index.GetOrCreateIndex(collectionId);
        }

        protected Stream CreateReadWriteStream(string fileName)
        {
            return new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        }

        protected Stream CreateAppendStream(string fileName)
        {
            // https://stackoverflow.com/questions/122362/how-to-empty-flush-windows-read-disk-cache-in-c
            //const FileOptions FileFlagNoBuffering = (FileOptions)0x20000000;
            //FileStream file = new FileStream(fileName, fileMode, fileAccess, fileShare, blockSize,
            //    FileFlagNoBuffering | FileOptions.WriteThrough | fileOptions);

            return new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        }

        protected Stream CreateReadStream(string fileName)
        {
            if (File.Exists(fileName))
            {
                return new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            return null;
        }
    }
}
