using Microsoft.AspNetCore.Http;
using System;

namespace Sir.Store
{
    /// <summary>
    /// Parse text from a http request message into a query.
    /// </summary>
    public class HttpQueryParser
    {
        private readonly QueryParser _queryParser;

        public HttpQueryParser(QueryParser queryParser)
        {
            _queryParser = queryParser;
        }

        public Query Parse(ulong collectionId, IStringModel tokenizer, HttpRequest request)
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
                query = FromFormattedString(collectionId, formattedQuery, tokenizer);
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

                query = _queryParser.Parse(collectionId, formattedQuery, tokenizer);
            }

            if (request.Query.ContainsKey("take"))
                query.Take = int.Parse(request.Query["take"]);

            if (request.Query.ContainsKey("skip"))
                query.Skip = int.Parse(request.Query["skip"]);

            return query;
        }

        public Query FromFormattedString(ulong collectionId, string formattedQuery, IStringModel tokenizer)
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
                    .Replace(")", "\n")
                    .Replace("++", "+")
                    .Replace("--", "-");

                string[] terms;
                
                if (cleanLine.ContainsMany(':'))
                {
                    terms = cleanLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                }
                else
                {
                    terms = new string[] { cleanLine };
                }

                object lastField = string.Empty;

                foreach (var term in terms)
                {
                    var t = term;

                    if (!term.Contains(':'))
                    {
                        t = $"{lastField}:{term}";
                    }

                    var query = _queryParser.Parse(collectionId, t, tokenizer);

                    lastField = query.Term.Key;

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
                    var next = last.NextClause;

                    while (next != null)
                    {
                        last = next;
                        next = last.NextClause;
                    }

                    last.NextClause = x;
                }
            }

            return root;
        }
    }
}
