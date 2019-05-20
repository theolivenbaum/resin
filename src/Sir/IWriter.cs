using Microsoft.AspNetCore.Http;

namespace Sir
{
    /// <summary>
    /// Write data to a collection.
    /// </summary>
    public interface IWriter : IPlugin
    {
        ResponseModel Write(string collectionId, HttpRequest request);
    }
}
