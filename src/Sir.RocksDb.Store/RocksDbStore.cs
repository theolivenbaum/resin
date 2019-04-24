using System;
using System.Collections.Generic;
using RocksDbSharp;
using Sir.Core;

namespace Sir.RocksDb.Store
{
    public class RocksDbStore : IKeyValueStore, IDisposable
    {
        private readonly ProducerConsumerQueue<(byte[] key, byte[] value)> _writer;
        private readonly string _dir;

        public RocksDbStore(IConfigurationProvider config)
        {
            _dir = config.Get("data_dir");
            _writer = new ProducerConsumerQueue<(byte[] key, byte[] value)>(1, DoWrite);
        }

        private void DoWrite((byte[] key, byte[] value) data)
        {
            var options = new DbOptions().SetCreateIfMissing(true);

            using (var db = RocksDbSharp.RocksDb.Open(options, _dir))
            {
                db.Put(data.key, data.value);
            }
        }

        public byte[] Get(byte[] key)
        {
            var options = new DbOptions().SetCreateIfMissing(true);

            using (var db = RocksDbSharp.RocksDb.OpenReadOnly(options, _dir, false))
            {
                return db.Get(key);
            }
        }

        public IEnumerable<KeyValuePair<byte[], byte[]>> GetMany(byte[][] keys)
        {
            var options = new DbOptions().SetCreateIfMissing(true);

            using (var db = RocksDbSharp.RocksDb.OpenReadOnly(options, _dir, false))
            {
                return db.MultiGet(keys);
            }
        }

        public void Put(byte[] key, byte[] value)
        {
            _writer.Enqueue((key, value));
        }

        public void Dispose()
        {
            _writer.Dispose();
        }
    }
}
