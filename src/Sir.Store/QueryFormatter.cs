using Microsoft.AspNetCore.Http;

namespace Sir.Store
{
    public class QueryFormatter : IQueryFormatter
    {
        public string Format(string collectionName, HttpRequest request)
        {
            return new HttpQueryParser(new TermQueryParser(), new LatinTokenizer()).Parse(collectionName.ToHash(), request).ToString();
        }
    }
}
