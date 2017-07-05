using DocumentTable;
using Resin.Sys;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.IO.Read
{
    public class DocumentStoreReadSessionFactory : IDocumentStoreReadSessionFactory
    {
        public IDocumentStoreReadSession Create(string docAddressFileName, string docFileName, Compression compression)
        {
            var dir = Path.GetDirectoryName(docFileName);
            var version = Path.GetFileNameWithoutExtension(docFileName);
            var keyIndexFileName = Path.Combine(dir, version + ".kix");
            var keyIndex = TableSerializer.GetKeyIndex(keyIndexFileName);

            return new DocumentStoreReadSession(
                new DocumentAddressReader(new FileStream(docAddressFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan)),
                new DocumentReader(
                    new FileStream(docFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan), 
                    compression, 
                    keyIndex));
        }
    }
}
