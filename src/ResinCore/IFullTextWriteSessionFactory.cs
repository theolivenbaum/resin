using Resin.IO;
using Resin.Documents;

namespace Resin
{
    public interface IFullTextWriteSessionFactory : IWriteSessionFactory
    {
        IWriteSession OpenWriteSession(
            Compression compression, TreeBuilder treeBuilder);
    }
}