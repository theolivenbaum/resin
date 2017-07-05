using System.IO;

namespace DocumentTable
{
    public class WriteSessionFactory : IWriteSessionFactory
    {
        private readonly string _directory;
        private readonly BatchInfo _ix;
        private readonly Compression _compression;

        public WriteSessionFactory(string directory, BatchInfo ix, Compression compression)
        {
            _ix = ix;
            _directory = directory;
            _compression = compression;
        }

        public IWriteSession OpenWriteSession(Stream compoundFile)
        {
            return new WriteSession(_directory, _ix, _compression, compoundFile);
        }
    }
}