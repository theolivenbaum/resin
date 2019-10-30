using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Sir.Store
{
    /// <summary>
    /// Parse text from a http request message into a query.
    /// </summary>
    public class HttpQueryParser
    {
        private readonly QueryParser _parser;

        public HttpQueryParser(SessionFactory sessionFactory, IStringModel model)
        {
            _parser = new QueryParser(sessionFactory, model);
        }

        public IEnumerable<Query> Parse(ulong collectionId, HttpRequest request)
        {
            string[] fields = request.Query["field"].ToArray();
            bool and = request.Query.ContainsKey("AND");
            bool or = !and;
            const bool not = false;
            var isFormatted = request.Query.ContainsKey("qf");

            if (isFormatted)
            {
                var formattedQuery = request.Query["qf"].ToString();

                return FromFormattedString(collectionId, formattedQuery, and, or, not);
            }
            else
            {
                var naturalLanguage = request.Query["q"].ToString();

                return _parser.Parse(collectionId, naturalLanguage, fields, and, or, not);
            }
        }

        public IEnumerable<Query> FromFormattedString(ulong collectionId, string formattedQuery, bool and, bool or, bool not)
        {
            var document = JsonConvert.DeserializeObject<IDictionary<string, object>>(formattedQuery);

            return FromDocument(collectionId, document, and, or, not);
        }

        public IEnumerable<Query> FromDocument(ulong collectionId, IDictionary<string, object> document, bool and, bool or, bool not)
        {
            return _parser.Parse(collectionId, document, and, or, not);
        }
    }
}
