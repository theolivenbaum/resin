using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Sir
{
    public interface IReader : IPlugin
    {
       Task<Result> Read(string collectionId, HttpRequest request);
    }
}
