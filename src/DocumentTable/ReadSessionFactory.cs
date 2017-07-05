using System.IO;

namespace DocumentTable
{
    public class ReadSessionFactory : IReadSessionFactory
    {
        public IReadSession Create(string docAddressFileName, string docFileName, Compression compression)
        {
            var dir = Path.GetDirectoryName(docFileName);
            var version = Path.GetFileNameWithoutExtension(docFileName);
            var keyIndexFileName = Path.Combine(dir, version + ".kix");
            var keyIndex = TableSerializer.GetKeyIndex(keyIndexFileName);

            return new ReadSession(
                new DocumentAddressReader(new FileStream(docAddressFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan)),
                new DocumentReader(
                    new FileStream(docFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan), 
                    compression, 
                    keyIndex));
        }
    }
}
