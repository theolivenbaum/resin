using Microsoft.AspNetCore.Http;

namespace Sir.Store
{
    public class QueryFormatter : IQueryFormatter
    {
        public string Format(string collectionName, IStringModel tokenizer, HttpRequest request)
        {
            return new HttpQueryParser(new QueryParser())
                .Parse(collectionName.ToHash(), tokenizer, request).ToString();
        }
    }
}
