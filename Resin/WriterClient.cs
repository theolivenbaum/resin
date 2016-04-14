using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            _client.Timeout = TimeSpan.FromMinutes(10);
            _url = url + indexName + "/add";
        }

        public void Write(IEnumerable<IDictionary<string, string>> docs)
        {
            if(Post(new ArrayList(docs.ToArray())).Result != HttpStatusCode.NoContent) throw new InvalidOperationException();
        }

        private async Task<HttpStatusCode> Post(ArrayList docs)
        {
            var response = await _client.PostAsJsonAsync(_url, docs);
            return response.StatusCode;
        }

        public void Dispose()
        {
            if(_client != null) _client.Dispose();
        }
    }
}