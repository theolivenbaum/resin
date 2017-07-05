namespace DocumentTable
{
    public class WriteSessionFactory : IWriteSessionFactory
    {
        private readonly string _directory;
        private readonly long _indexVersionId;
        private readonly Compression _compression;

        public WriteSessionFactory(string directory, long indexVersionId, Compression compression)
        {
            _directory = directory;
            _indexVersionId = indexVersionId;
            _compression = compression;
        }

        public IWriteSession OpenWriteSession()
        {
            return new WriteSession(_directory, _indexVersionId, _compression);
        }
    }
}