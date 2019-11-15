using Microsoft.AspNetCore.Http;

namespace Sir
{
    public interface IQueryFormatter
    {
        string Format(HttpRequest request, IStringModel model);
    }
}