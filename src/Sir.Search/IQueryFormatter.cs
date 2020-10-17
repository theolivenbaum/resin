using Microsoft.AspNetCore.Http;
using Sir.Search;
using System.Threading.Tasks;

namespace Sir
{
    public interface IQueryFormatter
    {
        Task<string> Format(HttpRequest request, ITextModel model);
    }
}