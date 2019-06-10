using Microsoft.AspNetCore.Http;

namespace Sir.Store
{
    public class HttpBowQueryParser
    {
        private readonly HttpQueryParser _httpQueryParser;

        public HttpBowQueryParser(HttpQueryParser httpQueryParser)
        {
            _httpQueryParser = httpQueryParser;
        }

        public Query Parse(
            ulong collectionId, 
            HttpRequest request, 
            ReadSession readSession,
            IStringModel tokenizer)
        {
            var query = _httpQueryParser.Parse(collectionId, tokenizer, request);
            readSession.Map(query);
            return query;
        }
    }
}
