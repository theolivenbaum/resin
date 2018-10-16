using System.IO;
using System.Threading.Tasks;

namespace Sir
{
    public interface IWriter : IPlugin
    {
        Task<long> Write(ulong collectionId, Stream payload);

        void Append(ulong collectionId, long id, Stream payload);
    }
}
