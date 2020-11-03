using Microsoft.AspNetCore.Http;
using Sir.Search;

namespace Sir.HttpServer
{
    /// <summary>
    /// Write data to a collection.
    /// </summary>
    public interface IHttpWriter
    {
        void Write(HttpRequest request, ITextModel model);
    }
}
