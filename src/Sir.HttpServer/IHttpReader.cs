using Microsoft.AspNetCore.Http;
using Sir.Search;
using System.Threading.Tasks;

namespace Sir.HttpServer
{
    /// <summary>
    /// Read data from a collection.
    /// </summary>
    public interface IHttpReader
    {
       Task<SearchResult> Read(HttpRequest request, ITextModel model);
    }
}