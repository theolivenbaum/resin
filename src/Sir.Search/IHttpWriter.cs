using Microsoft.AspNetCore.Http;
using Sir.Search;

namespace Sir
{
    /// <summary>
    /// Write data to a collection.
    /// </summary>
    public interface IHttpWriter : IPlugin
    {
        void Write(HttpRequest request, ITextModel model);
    }
}
