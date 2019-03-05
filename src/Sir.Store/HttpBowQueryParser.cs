using Microsoft.AspNetCore.Http;

namespace Sir.Store
{
    public class HttpBowQueryParser
    {
        private readonly ITokenizer _tokenizer;

        public HttpBowQueryParser(ITokenizer tokenizer)
        {
            _tokenizer = tokenizer;
        }

        public Query Parse(
            ulong collectionId, 
            HttpRequest request, 
            ReadSession readSession, 
            SessionFactory sessionFactory)
        {
            string[] fields;

            if (request.Query.ContainsKey("fields"))
            {
                fields = request.Query["fields"].ToArray();
            }
            else
            {
                fields = new[] { "title", "body" };
            }

            var phrase = request.Query["q"];
            Query query = null;
            Query root = null;

            foreach (var field in fields)
            {
                var keyId = sessionFactory.GetKeyId(collectionId, field.ToLower().ToHash());
                var vector = BOWWriteSession.CreateDocumentVector(phrase, readSession.CreateIndexReader(keyId), _tokenizer);
                var q = new Query(collectionId, new Term(keyId, new VectorNode(vector)));

                if (query == null)
                {
                    query = q;
                    root = q;
                }
                else
                {
                    query.Next = q;
                }
            }

            int skip = 0;
            int take = 10;

            if (request.Query.ContainsKey("take"))
                take = int.Parse(request.Query["take"]);

            if (request.Query.ContainsKey("skip"))
                skip = int.Parse(request.Query["skip"]);

            root.Skip = skip;
            root.Take = take;

            return root;
        }
    }
}
