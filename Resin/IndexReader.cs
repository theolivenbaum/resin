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

        private readonly Dictionary<int, Dictionary<int, Dictionary<string, List<string>>>> _docFiles; // doc cache

        public Scanner Scanner { get { return _scanner; } }

        public IndexReader(Scanner scanner)
        {
            _scanner = scanner;
            var docIdToFileIndexFileName = Path.Combine(_scanner.Dir, "d.ix");
            _docFiles = new Dictionary<int, Dictionary<int, Dictionary<string, List<string>>>>();

            using (var file = File.OpenRead(docIdToFileIndexFileName))
            {
                _docIdToFileIndex = Serializer.Deserialize<Dictionary<int, int>>(file);
            }
        }

        public IEnumerable<Document> GetDocuments(string field, string token)
        {
            var docs = _scanner.GetDocIds(new Term {Field = field, Token = token});
            return docs.Select(GetDocFromDisk);
        }

        public IEnumerable<Document> GetDocuments(IList<Term> terms)
        {
            IList<DocumentScore> results = null;
            foreach (var term in terms)
            {
                var subResult = _scanner.GetDocIds(term).ToList();
                if (results == null)
                {
                    results = subResult;
                }
                else
                {
                    if (term.And)
                    {
                        results = results.Intersect(subResult).ToList();
                    }
                    else if (term.Not)
                    {
                        results = results.Except(subResult).ToList();
                    }
                    else
                    {
                        // Or
                        results = results.Concat(subResult).Distinct().ToList();
                    }
                }
            }

            var scored = new List<DocumentScore>();
            if (results != null)
            {
                foreach (var doc in results.GroupBy(d=>d.DocId))
                {
                    float documentSignificance = 0;
                    foreach (var subScore in doc)
                    {
                        documentSignificance += subScore.Value;
                    }
                    scored.Add(new DocumentScore{DocId = doc.Key, Value = documentSignificance});
                }

                foreach (var doc in scored)
                {
                    yield return GetDocFromDisk(doc);
                }
            }
        }

        private Document GetDocFromDisk(DocumentScore doc)
        {
            var fileId = _docIdToFileIndex[doc.DocId];
            Dictionary<int, Dictionary<string, List<string>>> dics;
            if (!_docFiles.TryGetValue(fileId, out dics))
            {
                dics = ReadDocFile(Path.Combine(_scanner.Dir, fileId + ".d"));
                _docFiles.Add(fileId, dics);
            }
            var dic = dics[doc.DocId];
            var d = Document.FromDictionary(doc.DocId, dic);
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