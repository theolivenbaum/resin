using Microsoft.AspNetCore.Http;
using Sir.Search;
using Sir.VectorSpace;
using System.Threading.Tasks;

namespace Sir.HttpServer
{
    /// <summary>
    /// Read data from a collection.
    /// </summary>
    public interface IHttpReader
    {
       Task<SearchResult> Read(HttpRequest request, IModel<string> model);
    }
}