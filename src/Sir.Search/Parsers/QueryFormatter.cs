using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Sir.Store
{
    public class QueryFormatter : IQueryFormatter
    {
        private readonly SessionFactory _sessionFactory;

        public QueryFormatter(SessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        public string Format(string collectionName, IStringModel tokenizer, HttpRequest request)
        {
            return JsonConvert.SerializeObject(new HttpQueryParser(_sessionFactory, tokenizer)
                .Parse(request), Formatting.Indented);
        }
    }
}
