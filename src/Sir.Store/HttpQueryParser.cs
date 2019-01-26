using Microsoft.AspNetCore.Http;

namespace Sir.Store
{
    /// <summary>
    /// Parse a query from a http request message.
    /// </summary>
    public class HttpQueryParser
    {
        private readonly KeyValueBooleanQueryParser _queryParser;

        public HttpQueryParser(KeyValueBooleanQueryParser queryParser)
        {
            _queryParser = queryParser;
        }

        public Query Parse(string collectionId, HttpRequest request, ITokenizer tokenizer)
        {
            Query query = null;

            string[] fields;

            bool and = request.Query.ContainsKey("AND");

            if (request.Query.ContainsKey("fields"))
            {
                fields = request.Query["fields"].ToArray();
            }
            else
            {
                fields = new[] { "title", "body" };
            }

            string queryFormat = string.Empty;

            if (request.Query.ContainsKey("format"))
            {
                queryFormat = request.Query["format"].ToArray()[0];
            }
            else
            {
                foreach (var field in fields)
                {
                    queryFormat += (field + ":{0}\n");
                }

                queryFormat = queryFormat.Substring(0, queryFormat.Length - 1);
            }

            if (!string.IsNullOrWhiteSpace(request.Query["q"]))
            {
                var expandedQuery = string.Format(queryFormat, request.Query["q"]);

                query = _queryParser.Parse(expandedQuery, and, !and, tokenizer);
                query.Collection = collectionId.ToHash();

                if (request.Query.ContainsKey("take"))
                    query.Take = int.Parse(request.Query["take"]);

                if (request.Query.ContainsKey("skip"))
                    query.Skip = int.Parse(request.Query["skip"]);
            }

            return query;
        }
    }
}
