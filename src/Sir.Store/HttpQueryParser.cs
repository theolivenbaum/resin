using Microsoft.AspNetCore.Http;

namespace Sir.Store
{
    public class HttpQueryParser
    {
        private readonly BooleanKeyValueQueryParser _queryParser;

        public HttpQueryParser(BooleanKeyValueQueryParser queryParser)
        {
            _queryParser = queryParser;
        }

        public Query Parse(string collectionId, HttpRequest request, ITokenizer tokenizer)
        {
            Query query = null;

            if (!string.IsNullOrWhiteSpace(request.Query["q"]))
            {
                var expandedQuery = string.Format("title:{0}\nbody:{0}", request.Query["q"]);

                query = _queryParser.Parse(expandedQuery, tokenizer);
                query.Collection = collectionId.ToHash();

                if (request.Query.ContainsKey("take"))
                    query.Take = int.Parse(request.Query["take"]);
            }

            return query;
        }
    }
}
