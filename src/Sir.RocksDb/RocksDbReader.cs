using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RocksDbSharp;

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
            var type = request.Query["type"].ToString();
            var typeId = type.ToHash();
            var path = Path.Combine(_config.Get("data_dir"), $"{typeId}.{collectionId}.rocks");
            var id = BitConverter.GetBytes(long.Parse(request.Query["id"]));

            var options = new DbOptions().SetCreateIfMissing(true);
            byte[] response;

            using (var db = RocksDbSharp.RocksDb.Open(options, path))
            {
                response = db.Get(id);
            }

            return new ResponseModel { Stream = new MemoryStream(response, false), MediaType = "application/rocksdb+octet-stream" };
        }
    }
}
