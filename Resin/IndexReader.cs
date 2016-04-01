using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.IO;

namespace Resin
{
    public class IndexReader : IDisposable
    {
        private readonly FieldScanner _fieldScanner;

        // docid/files
        private readonly Dictionary<string, List<string>> _docFiles;

        // docid/doc
        private readonly IDictionary<string, IDictionary<string, string>> _docCache;
        
        private readonly string _directory;
        private readonly bool _cacheDocs;
        private static readonly object Sync = new object();

        public FieldScanner FieldScanner { get { return _fieldScanner; } }

        public IndexReader(string directory, bool cacheDocs = false)
        {
            _directory = directory;
            _cacheDocs = cacheDocs;
            _docFiles = new Dictionary<string, List<string>>();
            _docCache = new Dictionary<string, IDictionary<string, string>>();

            var ixIds = Directory.GetFiles(_directory, "*.ix")
                .Where(f => Path.GetExtension(f) != ".tmp")
                .Select(f => int.Parse(Path.GetFileNameWithoutExtension(f)))
                .OrderBy(i => i).ToList();

            foreach (var ixFileName in ixIds.Select(id => Path.Combine(_directory, id + ".ix")))
            {
                var ix = IxFile.Load(ixFileName);
                var dix = DixFile.Load(ix.DixFileName);

                foreach (var doc in dix.DocIdToFileIndex)
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
            var hits = new Dictionary<string, DocumentScore>();
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
                        var aggr = new Dictionary<string, DocumentScore>();
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

        public IDictionary<string, string> GetDoc(DocumentScore docScore)
        {
            IDictionary<string, string> doc;
            if (!_docCache.TryGetValue(docScore.DocId, out doc))
            {
                lock (Sync)
                {
                    if (!_docCache.TryGetValue(docScore.DocId, out doc))
                    {
                        doc = new Dictionary<string, string>();
                        foreach (var file in _docFiles[docScore.DocId])
                        {
                            var d = GetDoc(Path.Combine(_directory, file + ".d"), docScore.DocId);
                            if (d != null)
                            {
                                foreach (var field in d.Fields)
                                {
                                    doc[field.Key] = field.Value; // overwrites former value with latter
                                }
                            }
                        }
                        if (doc.Count == 0)
                        {
                            throw new ArgumentException("Document missing from index", "docScore");
                        }
                        if(_cacheDocs) _docCache[docScore.DocId] = doc;  
                    }
                }
            }
            return doc;
        }

        private Document GetDoc(string fileName, string docId)
        {
            var docs = DocFile.Load(fileName);
            Document doc;
            return docs.Docs.TryGetValue(docId, out doc) ? doc : null;
        }

        public void Dispose()
        {
        }
    }
}