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
        private readonly Dictionary<int, Dictionary<int, Dictionary<string, List<string>>>> _docFiles;

        public Scanner Scanner { get { return _scanner; } }

        public IndexReader(Scanner scanner)
        {
            _scanner = scanner;
            _docIdToFileIndexFileName = Path.Combine(_scanner.Dir, "d.ix");
            _docs = new Dictionary<int, Dictionary<string, List<string>>>();
            _docFiles = new Dictionary<int, Dictionary<int, Dictionary<string, List<string>>>>();

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

        private int SaveInHotFile(int docId, Dictionary<string, List<string>> doc)
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
            Dictionary<string, List<string>> dic;
            if (!_docs.TryGetValue(docId, out dic))
            {
                var fileId = _docIdToFileIndex[docId];
                Dictionary<int, Dictionary<string, List<string>>> dics;
                if (!_docFiles.TryGetValue(fileId, out dics))
                {
                    dics = ReadDocFile(Path.Combine(_scanner.Dir, fileId + ".d"));
                    _docFiles.Add(fileId, dics);
                    //if (dics.Count > 1000)
                    //{
                    //    _docIdToFileIndex[docId] = Serialize(docId, dic);
                    //    Serialize(_docIdToFileIndex);
                    //}
                }
                dic = dics[docId];
                _docs.Add(docId, dic);
            }
            var d = Document.FromDictionary(docId, dic);
            return d;
        }

        private Dictionary<int, Dictionary<string, List<string>>> ReadDocFile(string fileName)
        {
            using (var file = File.OpenRead(fileName))
            {
                return Serializer.Deserialize<Dictionary<int, Dictionary<string, List<string>>>>(file);
            }
        }

        public void Dispose()
        {
        }
    }
}