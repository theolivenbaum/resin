using Microsoft.AspNetCore.Http;

namespace Sir.Store
{
    public class HttpBowQueryParser
    {
        private readonly ITokenizer _tokenizer;
        private readonly HttpQueryParser _httpQueryParser;

        public HttpBowQueryParser(ITokenizer tokenizer, HttpQueryParser httpQueryParser)
        {
            _tokenizer = tokenizer;
            _httpQueryParser = httpQueryParser;
        }

        public Query Parse(
            ulong collectionId, 
            HttpRequest request, 
            ReadSession mappingSession)
        {
            var query = _httpQueryParser.Parse(collectionId, request);
            mappingSession.Map(query);
            return query;
        }
    }
}
