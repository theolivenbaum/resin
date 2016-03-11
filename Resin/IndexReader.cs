using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class IndexReader
    {
        private readonly Scanner _scanner;
        private readonly IDictionary<int, int> _docIdToFileIndex;
        public IndexReader(Scanner scanner)
        {
            _scanner = scanner;
            var fileName = Path.Combine(_scanner.Dir, "d.ix");
            using (var file = File.OpenRead(fileName))
            {
                _docIdToFileIndex = Serializer.Deserialize<IDictionary<int, int>>(file);
            }
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
            var fileId = _docIdToFileIndex[docId];
            var fileName = Path.Combine(_scanner.Dir, fileId + ".d");
            Dictionary<int, Dictionary<string, IList<string>>> docs;
            using (var file = File.OpenRead(fileName))
            {
                docs = Serializer.Deserialize<Dictionary<int, Dictionary<string, IList<string>>>>(file);
            }
            var doc = docs[docId];
            return Document.FromDictionary(docId, doc);
        }
    }
}