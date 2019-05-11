using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Sir.RockDb.Store
{
    public class VectorDb : IDisposable
    {
        private IKeyValueStore _db;

        public VectorDb(IConfigurationProvider config, IKeyValueStore db)
        {
            _db = db;
        }

        public async Task<long> Put(SortedList<long, int> vector)
        {
            var payload = new MemoryStream();

            await vector.SerializeAsync(payload);

            var key = Guid.NewGuid().ToString().ToHash().MapToLong();

            _db.Put(BitConverter.GetBytes(key), payload.ToArray());

            return key;
        }

        public async Task<SortedList<long, int>> Get(long key)
        {
            var buffer = _db.Get(BitConverter.GetBytes(key));

            if (buffer != null)
            {
                return await VectorOperations.DeserializeVectorAsync(0, new MemoryStream(buffer));
            }

            return null;
        }

        public void Dispose()
        {
            _db.Dispose();
        }
    }
}
