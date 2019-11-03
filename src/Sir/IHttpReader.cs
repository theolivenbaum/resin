using Microsoft.AspNetCore.Http;

namespace Sir
{
    /// <summary>
    /// Read data from a collection.
    /// </summary>
    public interface IHttpReader : IPlugin
    {
       ResponseModel Read(HttpRequest request, IStringModel model);
    }
}
