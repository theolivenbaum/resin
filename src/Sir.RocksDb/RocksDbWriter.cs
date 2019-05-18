using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Sir.RocksDb
{
    public class RocksDbWriter : IWriter
    {
        private readonly IConfigurationProvider _config;
        private readonly IKeyValueStore _store;
        private readonly object _writeLock = new object();

        public string ContentType => "application/rocksdb+octet-stream";

        public RocksDbWriter(IConfigurationProvider config, IKeyValueStore store)
        {
            _config = config;
            _store = store;
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

            DoWrite(collectionId.ToHash(), typeId, id, requestStream.ToArray());

            var response = new MemoryStream();

            response.Write(id);

            return new ResponseModel { Stream = response, MediaType = "application/octet-stream" };
        }

        private void DoWrite(ulong collection, ulong type, byte[] key, byte[] value)
        {
            var fileId = $"{collection}.{type}";
            var path = Path.Combine(_config.Get("data_dir"), fileId);

            lock (_writeLock)
            {
                _store.Put(key, value);
            }
        }

        public void Dispose()
        {
            _store.Dispose();
        }
    }
}
