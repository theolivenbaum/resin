using Microsoft.AspNetCore.Http;

namespace Sir
{
    public interface IQueryFormatter
    {
        string Format(string collectionName, HttpRequest request);
    }
}
