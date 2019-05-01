using System;
using System.Collections.Generic;
using RocksDbSharp;

namespace Sir.RocksDb.Store
{
    public class RocksDbStore : IKeyValueStore, IDisposable
    {
        private readonly string _dir;
        private readonly RocksDbSharp.RocksDb _db;

        public RocksDbStore(IConfigurationProvider config)
        {
            _dir = config.Get("data_dir");

            var options = new DbOptions().SetCreateIfMissing(true);

            _db = RocksDbSharp.RocksDb.Open(options, _dir);
        }

        public byte[] Get(byte[] key)
        {
            return _db.Get(key);
        }

        public IEnumerable<KeyValuePair<byte[], byte[]>> GetMany(byte[][] keys)
        {
            return _db.MultiGet(keys);
        }

        public void Put(byte[] key, byte[] value)
        {
            _db.Put(key, value);
        }

        public void Dispose()
        {
            _db.Dispose();
        }
    }
}