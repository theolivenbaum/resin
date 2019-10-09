using Microsoft.AspNetCore.Http;

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
            return new HttpQueryParser(_sessionFactory, tokenizer)
                .Parse(collectionName.ToHash(), request).ToString();
        }
    }
}
