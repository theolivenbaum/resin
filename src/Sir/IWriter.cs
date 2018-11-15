using System.IO;
using System.Threading.Tasks;

namespace Sir
{
    public interface IWriter : IPlugin
    {
        Task<Result> Write(string collectionId, Stream request);
    }
}
