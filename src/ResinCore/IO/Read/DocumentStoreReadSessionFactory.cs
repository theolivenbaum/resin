using System.IO;

namespace Resin.IO.Read
{
    public class DocumentStoreReadSessionFactory : IDocumentStoreReadSessionFactory
    {
        public IDocumentStoreReadSession Create(string docAddressFileName, string docFileName, Compression compression)
        {
            return new DocumentStoreReadSession(new DocumentAddressReader(
                new FileStream(docAddressFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan)),
                new DocumentReader(new FileStream(docFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 4, FileOptions.SequentialScan), compression));
        }
    }
}
