using DocumentTable;
using System.IO;

namespace Resin
{
    public class FullTextWriteSessionFactory : IWriteSessionFactory
    {
        private readonly string _directory;
        private readonly FullTextSegmentInfo _ix;

        public FullTextWriteSessionFactory(string directory, FullTextSegmentInfo ix)
        {
            _ix = ix;
            _directory = directory;
        }

        public IWriteSession OpenWriteSession(Stream compoundFile)
        {
            return new FullTextWriteSession(_directory, _ix, compoundFile);
        }
    }
}