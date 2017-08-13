using System.IO;

namespace DocumentTable
{
    public class WriteSessionFactory : IWriteSessionFactory
    {
        private readonly string _directory;
        private readonly SegmentInfo _ix;

        public WriteSessionFactory(string directory, SegmentInfo ix)
        {
            _ix = ix;
            _directory = directory;

            Directory.SetCurrentDirectory(directory);
        }

        public IWriteSession OpenWriteSession(Stream compoundFile)
        {
            return new WriteSession(_directory, _ix, compoundFile);
        }
    }
}