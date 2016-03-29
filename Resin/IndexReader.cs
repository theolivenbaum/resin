using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class IndexReader : IDisposable
    {
        private readonly FieldScanner _fieldScanner;

        // docid/files
        private readonly Dictionary<int, List<string>> _docFiles;

        // docid/doc
        private readonly IDictionary<int, IDictionary<string, string>> _docCache;
        
        private readonly string _directory;

        public FieldScanner FieldScanner { get { return _fieldScanner; } }

        public IndexReader(string directory)
        {
            _directory = directory;
            _docFiles = new Dictionary<int, List<string>>();
            _docCache = new Dictionary<int, IDictionary<string, string>>();

            var ixIds = Directory.GetFiles(_directory, "*.ix")
                .Where(f => Path.GetExtension(f) != ".tmp")
                .Select(f => int.Parse(Path.GetFileNameWithoutExtension(f)))
                .OrderBy(i => i).ToList();

            foreach (var ixFileName in ixIds.Select(id => Path.Combine(_directory, id + ".ix")))
            {
                Index ix;
                using (var fs = File.OpenRead(ixFileName))
                {
                    ix = Serializer.Deserialize<Index>(fs);
                }

                IDictionary<int, string> dix;
                using (var fs = File.OpenRead(ix.DixFileName))
                {
                    dix = Serializer.Deserialize<Dictionary<int, string>>(fs);
                }

                foreach (var doc in dix)
                {
                    List<string> files;
                    if (_docFiles.TryGetValue(doc.Key, out files))
                    {
                        files.Add(doc.Value);
                    }
                    else
                    {
                        _docFiles.Add(doc.Key, new List<string>{doc.Value});
                    }
                }
            }
            _fieldScanner = FieldScanner.MergeLoad(_directory);
        }

        public IEnumerable<DocumentScore> GetScoredResult(IEnumerable<Term> terms)
        {
            var hits = new Dictionary<int, DocumentScore>();
            foreach (var term in terms)
            {
                var termHits = _fieldScanner.GetDocIds(term).ToList();
                if (termHits.Count == 0) continue;

                var docsInCorpus = _fieldScanner.DocCount(term.Field);
                var scorer = new Tfidf(docsInCorpus, termHits.Count);
                
                if (hits.Count == 0)
                {
                    if (!term.Not)
                    {
                        foreach (var doc in termHits)
                        {
                            scorer.Score(doc);
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
                            DocumentScore dscore;
                            if (hits.TryGetValue(doc.DocId, out dscore))
                            {
                                scorer.Score(dscore);
                                dscore.Score += doc.Score;
                                aggr.Add(dscore.DocId, dscore);
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
                            scorer.Score(doc);

                            DocumentScore score;
                            if (hits.TryGetValue(doc.DocId, out score))
                            {
                                score.Score += doc.Score;
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

        public Document GetDoc(DocumentScore docScore)
        {
            IDictionary<string, string> doc;
            if (!_docCache.TryGetValue(docScore.DocId, out doc))
            {
                doc = new Dictionary<string, string>();
                foreach (var file in _docFiles[docScore.DocId])
                {
                    var d = GetDoc(Path.Combine(_directory, file + ".d"), docScore.DocId);
                    if (d != null)
                    {
                        foreach (var field in d)
                        {
                            doc[field.Key] = field.Value; // overwrites former value with latter
                        }
                    }
                }
                if (doc.Count == 0)
                {
                    throw new ArgumentException("Document missing from index", "docScore");
                }
                _docCache[docScore.DocId] = doc;
            }
            return Document.FromDictionary(docScore.DocId, doc);
        }

        private Dictionary<string, string> GetDoc(string fileName, int docId)
        {
            var docs = ReadDocFile(fileName);
            Dictionary<string, string> doc;
            return docs.TryGetValue(docId, out doc) ? doc : null;
        }

        private Dictionary<int, Dictionary<string, string>> ReadDocFile(string fileName)
        {
            using (var file = File.OpenRead(fileName))
            {
                return Serializer.Deserialize<Dictionary<int, Dictionary<string, string>>>(file);
            }
        }

        public void Dispose()
        {
        }
    }
}