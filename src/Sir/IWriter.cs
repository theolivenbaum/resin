using System.IO;
using System.Threading.Tasks;

namespace Sir
{
    public interface IWriter : IPlugin
    {
        Task Write(string collectionId, Stream request, Stream response);

        Task Write(string collectionId, long id, Stream request, Stream response);
    }
}
