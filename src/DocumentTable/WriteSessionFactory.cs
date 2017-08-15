using System.IO;

namespace DocumentTable
{
    public class WriteSessionFactory : IWriteSessionFactory
    {
        private readonly string _directory;

        public WriteSessionFactory(string directory)
        {
            _directory = directory;

            Directory.SetCurrentDirectory(directory);
        }

        public IWriteSession OpenWriteSession(Compression compression)
        {
            return new WriteSession(_directory, compression);
        }
    }
}