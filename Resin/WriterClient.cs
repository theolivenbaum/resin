using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Resin
{
    public class WriterClient : IDisposable
    {
        private readonly string _url;
        private readonly HttpClient _client;

        public WriterClient(string indexName, string url)
        {
            _client = new HttpClient();
            _url = url + indexName + "/add";
        }

        public void Write(IDictionary<string, string> doc)
        {
            if(Post(doc).Result != HttpStatusCode.NoContent) throw new InvalidOperationException();
        }

        private async Task<HttpStatusCode> Post(IDictionary<string, string> doc)
        {
            var response = await _client.PostAsJsonAsync(_url, doc);
            return response.StatusCode;

        }

        public void Dispose()
        {
            if(_client != null) _client.Dispose();
        }
    }
}