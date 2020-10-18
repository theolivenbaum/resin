using Microsoft.AspNetCore.Http;

namespace Sir.Search
{
    /// <summary>
    /// Write data to a collection.
    /// </summary>
    public interface IHttpWriter
    {
        void Write(HttpRequest request, ITextModel model);
    }
}
