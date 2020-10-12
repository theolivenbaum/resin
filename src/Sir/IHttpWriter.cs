using Microsoft.AspNetCore.Http;

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
