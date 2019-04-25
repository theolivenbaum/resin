using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Sir.RocksDb.Store;

namespace Sir.RocksDb
{
    public class RocksDbWriter : IWriter
    {
        private readonly IConfigurationProvider _config;

        public string ContentType => "application/rocksdb+octet-stream";

        public RocksDbWriter(IConfigurationProvider config)
        {
            _config = config;
        }

        private void DoWrite((ulong collection, ulong type, byte[] key, byte[] value) data)
        {
            var fileId = $"{data.collection}.{data.type}";
            var path = Path.Combine(_config.Get("data_dir"), fileId);

            using (var store = new RocksDbStore(path))
            {
                store.Put(data.key, data.value);
            }
        }

        public void Dispose()
        {
        }

        public async Task<ResponseModel> Write(string collectionId, HttpRequest request)
        {
            var type = request.Query["type"].ToString();
            var typeId = type.ToHash();
            var path = Path.Combine(_config.Get("data_dir"), $"{typeId}.{collectionId}.rocks");

            var id = request.Query.ContainsKey("id") ? 
                BitConverter.GetBytes(long.Parse(request.Query["id"])) :
                BitConverter.GetBytes(Guid.NewGuid().ToString().ToHash());

            var requestStream = new MemoryStream();

            await request.Body.CopyToAsync(requestStream);

            DoWrite((collectionId.ToHash(), typeId, id, requestStream.ToArray()));

            var response = new MemoryStream();

            await response.WriteAsync(id);

            return new ResponseModel { Stream = response, MediaType = "application/octet-stream" };
        }
    }
}
