using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Sir.RocksDb.Store;

namespace Sir.RocksDb
{
    public class RocksDbReader : IReader
    {
        private readonly IConfigurationProvider _config;

        public string ContentType => "application/rocksdb+octet-stream";

        public RocksDbReader(IConfigurationProvider config)
        {
            _config = config;
        }

        public void Dispose()
        {
        }

        public async Task<ResponseModel> Read(string collectionId, HttpRequest request)
        {
            var typeId = request.Query["type"].ToString().ToHash();
            var path = Path.Combine(_config.Get("data_dir"), $"{typeId}.{collectionId.ToHash()}.rocks");
            var ids = request.Query["id"].ToArray().Select(s => BitConverter.GetBytes(long.Parse(s))).ToArray();

            using (var store = new RocksDbStore(path))
            {
                var payload = store.GetMany(ids);
                var response = new MemoryStream();

                foreach (var item in payload)
                {
                    await response.WriteAsync(item.Value);
                }

                return new ResponseModel { Stream = response, MediaType = "application/rocksdb+octet-stream" };
            }
        }
    }
}