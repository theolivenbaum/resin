//using System;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Threading.Tasks;
//using Newtonsoft.Json;

//namespace Resin.Client
//{
//    public class SearchClient : IDisposable
//    {
//        private readonly string _url;
//        private readonly HttpClient _client;

//        public SearchClient(string indexName, string url)
//        {
//            _url = url + indexName;
//            _client = new HttpClient {Timeout = TimeSpan.FromMinutes(10)};
//            _client.DefaultRequestHeaders.Accept.Clear();
//            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
//        }

//        public ResolvedResult Search(string query, int page = 0, int size = 10000)
//        {
//            var q = query.Replace(" ", "%20").Replace("+", "%2B").Replace(":", "%3A");
//            var url = string.Format("{0}/?query={1}&page={2}&size={3}", _url, q, page, size);
//            Task<string> result = GetResponseString(new Uri(url));
//            var resolved = JsonConvert.DeserializeObject<ResolvedResult>(result.Result);
//            return resolved;
//        }

//        private async Task<string> GetResponseString(Uri uri)
//        {
//            var response = await _client.GetAsync(uri);
//            response.EnsureSuccessStatusCode();
//            var contents = await response.Content.ReadAsStringAsync();
//            return contents;
//        }

//        public void Dispose()
//        {
//            if(_client != null) _client.Dispose();
//        }
//    }
//}