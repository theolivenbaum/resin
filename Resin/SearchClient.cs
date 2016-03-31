using EasyHttp.Http;

namespace Resin
{
    public class SearchClient
    {
        private readonly string _url;
        private readonly HttpClient _http;

        public SearchClient(string indexName, string url)
        {
            _url = url+ indexName;
            _http = new HttpClient();
            _http.Request.Accept = HttpContentTypes.ApplicationJson;
        }

        public DynamicResult Search(string query, int page = 0, int size = 10000)
        {
            var q = query.Replace(" ", "%20").Replace("+", "%2B");
            var url = string.Format("{0}/?query={1}&page={2}&size={3}", _url, q, page, size);
            var response = _http.Get(url);
            var result = response.DynamicBody;
            return new DynamicResult{Total = (int)result.total, Docs = result.docs};
        }
    }
}