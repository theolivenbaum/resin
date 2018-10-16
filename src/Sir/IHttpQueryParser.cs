using Microsoft.AspNetCore.Http;

namespace Sir
{
    public interface IHttpQueryParser : IPlugin
    {
        Query Parse(string collectionId, HttpRequest request, ITokenizer tokenizer);
    }
}
