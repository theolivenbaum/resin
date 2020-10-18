using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Sir.Search
{
    /// <summary>
    /// Read data from a collection.
    /// </summary>
    public interface IHttpReader
    {
       Task<ResponseModel> Read(HttpRequest request, ITextModel model);
    }
}
