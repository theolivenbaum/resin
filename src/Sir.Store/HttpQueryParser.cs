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

        public Query Parse(ulong collectionId, HttpRequest request, ITokenizer tokenizer)
        {
            Query query = null;

            if (!string.IsNullOrWhiteSpace(request.Query["q"]))
            {
                query = _queryParser.Parse(request.Query["q"], tokenizer);
                query.Collection = collectionId;

                if (request.Query.ContainsKey("take"))
                    query.Take = int.Parse(request.Query["take"]);
            }
            else if (!string.IsNullOrWhiteSpace(request.Query["id"]))
            {
                query = new Query("__docid", (string)request.Query["id"]);
                query.Collection = collectionId;
            }

            return query;
        }
    }
}
