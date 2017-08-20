using Resin.IO;
using Resin.Documents;

namespace Resin
{
    public class FullTextWriteSessionFactory : IFullTextWriteSessionFactory
    {
        private readonly string _directory;

        public FullTextWriteSessionFactory(string directory)
        {
            _directory = directory;
        }

        public IWriteSession OpenWriteSession(Compression compression, TreeBuilder treeBuilder)
        {
            return new FullTextWriteSession(_directory, compression, treeBuilder);
        }

        public IWriteSession OpenWriteSession(Compression compression)
        {
            return new WriteSession(_directory, compression);
        }
    }
}