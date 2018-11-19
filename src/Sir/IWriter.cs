using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Sir
{
    public interface IWriter : IPlugin
    {
        Task<Result> Write(string collectionId, HttpRequest request);
    }
}
