using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace Sir.Store
{
    /// <summary>
    /// Parse text from a http request message into a query.
    /// </summary>
    public class HttpQueryParser
    {
        private readonly TermQueryParser _queryParser;
        private readonly ITokenizer _tokenizer;

        public HttpQueryParser(TermQueryParser queryParser, ITokenizer tokenizer)
        {
            _queryParser = queryParser;
            _tokenizer = tokenizer;
        }

        public Query Parse(string collectionId, HttpRequest request)
        {
            Query query = null;

            string[] fields;

            bool and = request.Query.ContainsKey("AND");
            var termOperator = and ? "+" : "";

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
                    queryFormat += (termOperator + field + ":{0}\n");
                }

                queryFormat = queryFormat.Substring(0, queryFormat.Length - 1);
            }

            if (!string.IsNullOrWhiteSpace(request.Query["q"]))
            {
                var expandedQuery = string.Format(queryFormat, request.Query["q"]);

                query = _queryParser.Parse(expandedQuery, _tokenizer);
                query.Collection = collectionId.ToHash();

                if (request.Query.ContainsKey("take"))
                    query.Take = int.Parse(request.Query["take"]);

                if (request.Query.ContainsKey("skip"))
                    query.Skip = int.Parse(request.Query["skip"]);
            }

            return query;
        }
    }

    public class HttpBowQueryParser
    {
        private readonly ITokenizer _tokenizer;

        public HttpBowQueryParser(ITokenizer tokenizer)
        {
            _tokenizer = tokenizer;
        }

        public IDictionary<long, SortedList<int, byte>> Parse(
            string collectionId, HttpRequest request, ReadSession readSession, SessionFactory sessionFactory)
        {
            string[] fields;
            var docs = new Dictionary<long, SortedList<int, byte>>();

            if (request.Query.ContainsKey("fields"))
            {
                fields = request.Query["fields"].ToArray();
            }
            else
            {
                fields = new[] { "title", "body" };
            }

            var phrase = request.Query["q"];

            foreach (var field in fields)
            {
                var keyId = sessionFactory.GetKeyId(field.ToLower().ToHash());
                var vector = BOWWriteSession.CreateDocumentVector(phrase, readSession.CreateIndexReader(keyId), _tokenizer);

                docs.Add(keyId, vector);
            }

            return docs;
        }
    }
}
