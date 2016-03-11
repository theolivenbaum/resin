using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public IEnumerable<Document> GetDocuments(string field, string token)
        {
            var docs = _scanner.GetDocIds(field, token);
            foreach (var id in docs)
            {
                //TODO: page
                yield return GetDocFromDisk(id);
            }
        }

        public IEnumerable<Document> GetDocuments(IList<Term> terms)
        {
            IList<int> result = null;
            foreach (var term in terms)
            {
                var docs = _scanner.GetDocIds(term.Field, term.Token);
                if (result == null)
                {
                    result = docs;
                }
                else
                {
                    result = result.Intersect(docs).ToList(); // Intersect == AND
                }
            }
            if (result != null)
            {
                foreach (var id in result)
                {
                    //TODO: page
                    yield return GetDocFromDisk(id);
                } 
            }
            
        }

        private Document GetDocFromDisk(int docId)
        {
            var fileName = Path.Combine(_scanner.Dir, docId + ".d");
            using (var file = File.OpenRead(fileName))
            {
                return Document.FromDictionary(docId, Serializer.Deserialize<Dictionary<string, IList<string>>>(file));
            }
        }
    }
}