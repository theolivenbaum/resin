using Microsoft.AspNetCore.Http;

namespace Sir
{
    public interface IQueryFormatter
    {
        string Format(string collectionName, IModel model, HttpRequest request);
    }
}
