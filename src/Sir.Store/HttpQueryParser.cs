using Microsoft.AspNetCore.Http;
using System;
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

            var isFormatted = request.Query.ContainsKey("qf");

            if (isFormatted)
            {
                var formattedQuery = request.Query["qf"].ToString();
                query = FromString(formattedQuery);
            }
            else
            {
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

                var formattedQuery = string.Format(queryFormat, request.Query["q"]);

                query = _queryParser.Parse(formattedQuery, _tokenizer);
                query.Collection = collectionId.ToHash();
            }

            if (request.Query.ContainsKey("take"))
                query.Take = int.Parse(request.Query["take"]);

            if (request.Query.ContainsKey("skip"))
                query.Skip = int.Parse(request.Query["skip"]);

            return query;
        }

        private Query FromString(string formattedQuery)
        {
            Query root = null;
            var lines = formattedQuery
                .Replace("\r", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                Query x = null;

                var cleanLine = line
                    .Replace("(", "")
                    .Replace(")", "")
                    .Replace("++", "+")
                    .Replace("--", "-");

                var terms = cleanLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var term in terms)
                {
                    var query = _queryParser.Parse(term, _tokenizer);

                    if (x == null)
                    {
                        x = query;
                    }
                    else
                    {
                        x.AddClause(query);
                    }
                }

                if (root == null)
                {
                    root = x;
                }
                else
                {
                    var last = root;
                    var next = last.Next;

                    while (next != null)
                    {
                        last = next;
                        next = last.Next;
                    }

                    last.Next = x;
                }
            }

            return root;
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
            string collectionName, HttpRequest request, ReadSession readSession, SessionFactory sessionFactory)
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
                var keyId = sessionFactory.GetKeyId(collectionName.ToHash(), field.ToLower().ToHash());
                var vector = BOWWriteSession.CreateDocumentVector(phrase, readSession.CreateIndexReader(keyId), _tokenizer);

                docs.Add(keyId, vector);
            }

            return docs;
        }
    }
}
