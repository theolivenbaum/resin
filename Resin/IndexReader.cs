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
        
        public IEnumerable<DocumentScore> GetScoredResult(IEnumerable<Term> terms)
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

            if (results != null)
            {
                foreach (var group in results.GroupBy(d => d.DocId))
                {
                    yield return new DocumentScore { DocId = group.Key, Value = group.Sum(s=>s.Value) };
                }
            }
        }

        public Document GetDocFromDisk(DocumentScore doc)
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