using System.IO;

namespace DocumentTable
{
    public class ReadSessionFactory : IReadSessionFactory
    {
        public IReadSession OpenReadSession(string docAddressFileName, string docFileName, BatchInfo ix)
        {
            var dir = Path.GetDirectoryName(docFileName);
            var keyIndexFileName = Path.Combine(dir, ix.VersionId + ".kix");
            var keyIndex = TableSerializer.GetKeyIndex(keyIndexFileName);
            var compoundFileName = Path.Combine(dir, ix.VersionId + ".rdb");
            var compoundFile = new FileStream(compoundFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan);
            
                return new ReadSession(
                new DocumentAddressReader(compoundFile, ix.DocAddressesOffset),
                new DocumentReader(
                    new FileStream(docFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan), 
                    ix.Compression, 
                    keyIndex));
        }
    }
}
