using Microsoft.AspNetCore.Http;

namespace Sir
{
    public interface IQueryFormatter
    {
        string Format(string collectionName, IStringModel model, HttpRequest request);
    }
}
