using Microsoft.AspNetCore.Http;
using Sir.VectorSpace;
using System.Threading.Tasks;

namespace Sir.Search
{
    public interface IQueryFormatter<T>
    {
        Task<T> Format(HttpRequest request, IModel<T> model);
    }
}