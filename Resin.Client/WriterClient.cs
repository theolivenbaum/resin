using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Resin.Client
{
    public class WriterClient : IDisposable
    {
        private readonly string _url;
        private readonly HttpClient _client;

        public WriterClient(string indexName, string url)
        {
            _client = new HttpClient {Timeout = TimeSpan.FromMinutes(10)};
            _url = url + (url.EndsWith("/")? string.Empty : "/") + indexName + "/";
        }

        public void Write(IEnumerable<IDictionary<string, string>> docs)
        {
            if(Post(new ArrayList(docs.ToArray())).Result != HttpStatusCode.NoContent) throw new InvalidOperationException();
        }

        public void Remove(string docId)
        {
            if (Post(docId).Result != HttpStatusCode.NoContent) throw new InvalidOperationException();
        }

        private async Task<HttpStatusCode> Post(string docId)
        {
            var response = await _client.PostAsync(new Uri(_url + "remove/" + docId), new StringContent(docId));
            return response.StatusCode;
        }

        private async Task<HttpStatusCode> Post(ArrayList docs)
        {
            var response = await _client.PostAsJsonAsync(_url + "add", docs);
            return response.StatusCode;
        }

        public void Dispose()
        {
            if(_client != null) _client.Dispose();
        }
    }
}