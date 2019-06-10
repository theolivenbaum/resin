using Microsoft.AspNetCore.Http;

namespace Sir.Store
{
    public class QueryFormatter : IQueryFormatter
    {
        public string Format(string collectionName, IModel tokenizer, HttpRequest request)
        {
            return new HttpQueryParser(new QueryParser())
                .Parse(collectionName.ToHash(), tokenizer, request).ToString();
        }
    }
}
