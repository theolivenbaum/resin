using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Sir
{
    /// <summary>
    /// Write data to a collection.
    /// </summary>
    public interface IWriter : IPlugin
    {
        Task<ResponseModel> Write(string collectionId, HttpRequest request);
    }
}
