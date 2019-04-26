using System;
using System.Collections.Generic;
using RocksDbSharp;
using Sir.Core;

namespace Sir.RocksDb.Store
{
    public class RocksDbStore : IKeyValueStore, IDisposable
    {
        private readonly ProducerConsumerQueue<(byte[] key, byte[] value)> _writeQueue;
        private readonly RocksDbSharp.RocksDb _db;
        private readonly string _dir;

        public RocksDbStore(string dir)
        {
            _dir = dir;

            var options = new DbOptions().SetCreateIfMissing(true);

            _db = RocksDbSharp.RocksDb.Open(options, _dir);
            _writeQueue = new ProducerConsumerQueue<(byte[] key, byte[] value)>(1, DoWrite);
        }

        private void DoWrite((byte[] key, byte[] value) data)
        {
            _db.Put(data.key, data.value);
        }

        public byte[] Get(byte[] key)
        {
            return _db.Get(key);
        }

        public IEnumerable<KeyValuePair<byte[], byte[]>> GetMany(byte[][] keys)
        {
            return _db.MultiGet(keys);
        }

        public IEnumerable<KeyValuePair<byte[], byte[]>> GetAll()
        {
            var it = _db.NewIterator();

            for (it = it.SeekToFirst(); it.Valid(); it.Next())
            {
                yield return new KeyValuePair<byte[], byte[]>(it.Key(), it.Value());
            }

            it.Dispose();
        }

        public void Put(byte[] key, byte[] value)
        {
            _writeQueue.Enqueue((key, value));
        }

        public void Dispose()
        {
            _writeQueue.Dispose();
            _db.Dispose();
        }
    }
}
