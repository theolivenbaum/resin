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
        public int TotalNumberOfDocs { get { return _docIdToFileIndex.Count; } }

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
            var hits = new Dictionary<int, DocumentScore>();
            foreach (var term in terms)
            {
                var termHits = _scanner.GetDocIds(term).ToList();
                if (termHits.Count == 0) continue;

                var idf = Math.Log((double) TotalNumberOfDocs/(1+termHits.Count));
                
                if (hits.Count == 0)
                {
                    if (!term.Not)
                    {
                        foreach (var doc in termHits)
                        {
                            var tfidf = doc.TermFrequency * idf;
                            doc.Score = tfidf;
                        }
                        hits = termHits.ToDictionary(h => h.DocId, h => h);
                    }
                    
                }
                else
                {
                    if (term.And)
                    {
                        var aggr = new Dictionary<int, DocumentScore>();
                        foreach (var doc in termHits)
                        {
                            DocumentScore score;
                            if (hits.TryGetValue(doc.DocId, out score))
                            {
                                var tfidf = doc.TermFrequency*idf;
                                score.Score += tfidf;
                                aggr.Add(score.DocId, score);
                            }
                        }
                        hits = aggr;
                    }
                    else if (term.Not)
                    {
                        foreach (var doc in termHits)
                        {
                            hits.Remove(doc.DocId);
                        }
                    }
                    else // Or
                    {
                        foreach (var doc in termHits)
                        {
                            var tfidf = doc.TermFrequency * idf;
                            doc.Score = tfidf;
                            DocumentScore score;
                            if (hits.TryGetValue(doc.DocId, out score))
                            {
                                score.Score += tfidf;
                            }
                            else
                            {
                                hits.Add(doc.DocId, doc);
                            }
                        }
                    }
                }
            }
            return hits.Values;
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