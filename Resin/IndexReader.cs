using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class IndexReader
    {
        private readonly DocumentScanner _scanner;

        public IndexReader(DocumentScanner scanner)
        {
            _scanner = scanner;
        }

        public IEnumerable<IDictionary<string, IList<string>>> GetDocuments(string field, string value)
        {
            foreach(var docId in _scanner.GetDocIds(field, value))
            {
                var fileName = Path.Combine(_scanner.Dir, docId + ".d");
                using (var file = File.OpenRead(fileName))
                {
                    yield return Serializer.Deserialize<Dictionary<string, IList<string>>>(file);
                }
            }
        }
    }
}