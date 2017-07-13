using System.IO;

namespace DocumentTable
{
    public class WriteSessionFactory : IWriteSessionFactory
    {
        private readonly string _directory;
        private readonly BatchInfo _ix;

        public WriteSessionFactory(string directory, BatchInfo ix)
        {
            _ix = ix;
            _directory = directory;
        }

        public IWriteSession OpenWriteSession(Stream compoundFile)
        {
            return new WriteSession(_directory, _ix, compoundFile);
        }
    }
}