using System;
using Microsoft.AspNetCore.Http;

namespace Sir.Store
{
    public class HttpQueryParser : IHttpQueryParser
    {
        public string ContentType => "*";

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Query Parse(string collectionId, HttpRequest request, ITokenizer tokenizer)
        {
            var parser = new BooleanKeyValueQueryParser();
            Query query = null;

            if (!string.IsNullOrWhiteSpace(request.Query["q"]))
            {
                query = parser.Parse(request.Query["q"], tokenizer);
                query.Collection = collectionId.ToHash();

                if (request.Query.ContainsKey("take"))
                    query.Take = int.Parse(request.Query["take"]);
            }
            else if (!string.IsNullOrWhiteSpace(request.Query["id"]))
            {
                query = new Query("__docid", (string)request.Query["id"]);
                query.Collection = collectionId.ToHash();
            }

            return query;
        }
    }
}
