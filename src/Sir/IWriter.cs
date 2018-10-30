using System.IO;
using System.Threading.Tasks;

namespace Sir
{
    public interface IWriter : IPlugin
    {
        Task<long> Write(string collectionId, Stream payload);

        Task Write(string collectionId, long id, Stream payload);
    }
}
