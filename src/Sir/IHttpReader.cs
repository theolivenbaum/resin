using Microsoft.AspNetCore.Http;

namespace Sir
{
    /// <summary>
    /// Read data from a collection.
    /// </summary>
    public interface IHttpReader : IPlugin
    {
       ResponseModel Read(string collectionId, IStringModel model, HttpRequest request);
    }
}
