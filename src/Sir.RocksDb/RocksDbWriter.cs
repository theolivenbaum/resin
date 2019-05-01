using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Sir.Core;

namespace Sir.RocksDb
{
    public class RocksDbWriter : IWriter
    {
        private readonly IConfigurationProvider _config;
        private readonly IKeyValueStore _store;
        private readonly Semaphore _writeLock;

        public string ContentType => "application/rocksdb+octet-stream";

        public RocksDbWriter(IConfigurationProvider config, IKeyValueStore store)
        {
            _config = config;
            _store = store;
            _writeLock = new Semaphore(1, 2, "Sir.RocksDb");
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

            _writeLock.WaitOne();

            DoWrite(collectionId.ToHash(), typeId, id, requestStream.ToArray());

            _writeLock.Release();

            var response = new MemoryStream();

            await response.WriteAsync(id);

            return new ResponseModel { Stream = response, MediaType = "application/octet-stream" };
        }

        private void DoWrite(ulong collection, ulong type, byte[] key, byte[] value)
        {
            var fileId = $"{collection}.{type}";
            var path = Path.Combine(_config.Get("data_dir"), fileId);

            _store.Put(key, value);
        }

        public void Dispose()
        {
            _writeLock.Dispose();
            _store.Dispose();
        }
    }
}
