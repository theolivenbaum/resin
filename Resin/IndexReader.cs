using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class IndexReader : IDisposable
    {
        private readonly Scanner _scanner;
        private readonly Dictionary<int, int> _docIdToFileIndex;
        private readonly string _docIdToFileIndexFileName;
        private readonly Dictionary<int, Dictionary<string, List<string>>> _docs;

        public IndexReader(Scanner scanner)
        {
            _scanner = scanner;
            _docIdToFileIndexFileName = Path.Combine(_scanner.Dir, "d.ix");
            _docs = new Dictionary<int, Dictionary<string, List<string>>>();

            using (var file = File.OpenRead(_docIdToFileIndexFileName))
            {
                _docIdToFileIndex = Serializer.Deserialize<Dictionary<int, int>>(file);
            }
        }

        public IEnumerable<Document> GetDocuments(string field, string token)
        {
            var docs = _scanner.GetDocIds(field, token);
            foreach (var id in docs)
            {
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
                    yield return GetDocFromDisk(id);
                } 
            }
            
        }

        private int Serialize(int docId, Dictionary<string, List<string>> doc)
        {
            var id = Directory.GetFiles(_scanner.Dir, "*.d").Length;
            var fileName = Path.Combine(_scanner.Dir, id + ".d");
            File.WriteAllText(fileName, "");
            var docs = new Dictionary<int, Dictionary<string, List<string>>>();
            docs.Add(docId, doc);
            using (var fs = File.Create(fileName))
            {
                Serializer.Serialize(fs, docs);
            }
            return id;
        }

        private void Serialize(Dictionary<int, int> docIdToFileIndex)
        {
            using (var fs = File.Create(_docIdToFileIndexFileName))
            {
                Serializer.Serialize(fs, docIdToFileIndex);
            }
        }

        private Document GetDocFromDisk(int docId)
        {
            Dictionary<string, List<string>> doc;
            if (!_docs.TryGetValue(docId, out doc))
            {
                var fileId = _docIdToFileIndex[docId];
                var fileName = Path.Combine(_scanner.Dir, fileId + ".d");
                Dictionary<int, Dictionary<string, List<string>>> docs;
                using (var file = File.OpenRead(fileName))
                {
                    docs = Serializer.Deserialize<Dictionary<int, Dictionary<string, List<string>>>>(file);
                }
                doc = docs[docId];
                if (docs.Count > 1)
                {
                    _docIdToFileIndex[docId] = Serialize(docId, doc);
                    Serialize(_docIdToFileIndex);
                }
                _docs.Add(docId, doc);
            }
            return Document.FromDictionary(docId, doc);
        }

        public void Dispose()
        {
        }
    }
}