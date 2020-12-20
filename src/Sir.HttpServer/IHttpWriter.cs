using Microsoft.AspNetCore.Http;
using Sir.VectorSpace;

namespace Sir.HttpServer
{
    /// <summary>
    /// Write data to a collection.
    /// </summary>
    public interface IHttpWriter
    {
        void Write(HttpRequest request, IModel<string> model);
    }
}
