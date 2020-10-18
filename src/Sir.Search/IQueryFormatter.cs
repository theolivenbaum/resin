using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Sir.Search
{
    public interface IQueryFormatter
    {
        Task<string> Format(HttpRequest request, ITextModel model);
    }
}