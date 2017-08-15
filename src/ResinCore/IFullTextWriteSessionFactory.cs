using DocumentTable;
using Resin.IO;

namespace Resin
{
    public interface IFullTextWriteSessionFactory : IWriteSessionFactory
    {
        IWriteSession OpenWriteSession(
            Compression compression, TreeBuilder treeBuilder);
    }
}