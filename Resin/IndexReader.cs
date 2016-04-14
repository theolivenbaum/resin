using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
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
        private static readonly ILog Log = LogManager.GetLogger(typeof(IndexReader));

        public FieldScanner FieldScanner { get { return _fieldScanner; } }

        public IndexReader(string directory)
        {
            _directory = directory;
            _docFiles = new Dictionary<string, List<string>>();
            _docCache = new Dictionary<string, IDictionary<string, string>>();

            var ixIds = Directory.GetFiles(_directory, "*.ix")
                .Where(f => Path.GetExtension(f) != ".tmp")
                .Select(f => int.Parse(Path.GetFileNameWithoutExtension(f) ?? "-1"))
                .OrderBy(i => i).ToList();

            foreach (var ixFileName in ixIds.Select(id => Path.Combine(_directory, id + ".ix")))
            {
                var ix = IxFile.Load(Path.Combine(_directory, ixFileName));
                var dix = DixFile.Load(Path.Combine(_directory, ix.DixFileName));

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

        public IEnumerable<DocumentScore> GetScoredResult(Query query)
        {
            _fieldScanner.Expand(query);
            Fetch(query);
            return query.Resolve().Values.OrderByDescending(s => s.Score);
        }

        private IEnumerable<DocumentScore> GetScoredTermResult(Term term)
        {
            var result = _fieldScanner.GetDocIds(term).ToList();
            var numDocs = _fieldScanner.DocsInCorpus(term.Field);
            var scorer = new Tfidf(numDocs, result.Count);
            foreach (var hit in result)
            {
                scorer.Score(hit);
                Log.DebugFormat("score {0} {1}", hit.Score, hit.Trace);
            }
            return result;
        }

        private void Fetch(Query query)
        {
            query.Result = GetScoredTermResult(query).ToDictionary(x => x.DocId, y => y);
            foreach (var child in query.Children)
            {
                Fetch(child);
            }
        }

        public IDictionary<string, string> GetDoc(DocumentScore docScore)
        {
            var doc = new Dictionary<string, string>();
            foreach (var file in _docFiles[docScore.DocId])
            {
                var d = GetDoc(docScore.DocId);
                if (d == null)
                {
                    LoadFile(Path.Combine(_directory, file + ".d"));
                    d = GetDoc(docScore.DocId);
                }
                if (d == null)
                {
                    throw new ArgumentException("Document missing from index", "docScore");
                }
                Log.DebugFormat("found doc {0} in {1}", docScore.DocId, file);
                foreach (var field in d)
                {
                    doc[field.Key] = field.Value; // overwrites former value with latter
                }
            }
            return doc;
        }

        private IDictionary<string, string> GetDoc(string docId)
        {
            IDictionary<string, string> doc;
            if (!_docCache.TryGetValue(docId, out doc))
            {
                return null;
            }
            return doc;
        }

        private void LoadFile(string fileName)
        {
            foreach (var doc in DocFile.Load(fileName).Docs.Values)
            {
                _docCache[doc.Id] = doc.Fields;
            }
            Log.DebugFormat("cached {0}", fileName);
        }

        public void Dispose()
        {
        }
    }
}