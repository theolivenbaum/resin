using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.Sys;

namespace Resin
{
    public class IndexBuilder
    {
        private readonly IAnalyzer _analyzer;
        private readonly IEnumerable<Document> _documents;
        private static readonly ILog Log = LogManager.GetLogger(typeof(IndexBuilder));
        private readonly ConcurrentDictionary<string, int> _docCountByField;
        private readonly string _indexName;
        private readonly Dictionary<string, LcrsTrie> _tries;

        public IndexBuilder(IAnalyzer analyzer, IEnumerable<Document> documents)
        {
            _analyzer = analyzer;
            _documents = documents;
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();
            _indexName = ToolBelt.GetChronologicalFileId();
        }

        public Index ToIndex()
        {
            var data = _documents.ToList();
            var analyzeTime = Time();
            var analyzedDocs = Analyze(data);
            var postings = BuildPostingsMatrix(analyzedDocs);
            var info = CreateIxInfo();

            Log.DebugFormat("analyzed documents in {0}", analyzeTime.Elapsed);

            return new Index(info, data, postings, _tries);
        }

        private IList<AnalyzedDocument> Analyze(IEnumerable<Document> documents)
        {
            var analyzedDocs = new List<AnalyzedDocument>();

            using (var trieBuilder = new TaskQueue<AnalyzedDocument>(1, BuildTree))
            {
                foreach (var doc in documents)
                {
                    var analyzedDoc = _analyzer.AnalyzeDocument(doc);
                    analyzedDocs.Add(analyzedDoc);

                    trieBuilder.Enqueue(analyzedDoc);

                    foreach (var field in doc.Fields)
                    {
                        _docCountByField.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
                    }
                }
            }

            return analyzedDocs;
        }

        private IxInfo CreateIxInfo()
        {
            return new IxInfo
            {
                Name = _indexName,
                DocumentCount = new DocumentCount(new Dictionary<string, int>(_docCountByField))
            };
        }

        private void BuildTree(AnalyzedDocument analyzedDoc)
        {
            foreach (var term in analyzedDoc.Terms)
            {
                WriteToTrie(term.Key.Field, term.Key.Word.Value);
            }
        }

        private void WriteToTrie(string field, string value)
        {
            if (field == null) throw new ArgumentNullException("field");
            if (value == null) throw new ArgumentNullException("value");

            var trie = GetTrie(field);
            trie.Add(value);
        }

        private LcrsTrie GetTrie(string field)
        {
            LcrsTrie trie;
            if (!_tries.TryGetValue(field, out trie))
            {
                trie = new LcrsTrie('\0', false);
                _tries[field] = trie;
            }
            return trie;
        }

        private Dictionary<Term, List<DocumentPosting>> BuildPostingsMatrix(IList<AnalyzedDocument> analyzedDocs)
        {
            var postingsMatrix = new Dictionary<Term, List<DocumentPosting>>();

            foreach (var doc in analyzedDocs)
            {
                foreach (var term in doc.Terms)
                {
                    List<DocumentPosting> weights;

                    if (postingsMatrix.TryGetValue(term.Key, out weights))
                    {
                        weights.Add(new DocumentPosting(doc.Id, term.Value));
                    }
                    else
                    {
                        postingsMatrix.Add(term.Key, new List<DocumentPosting> { new DocumentPosting(doc.Id, term.Value) });
                    }
                }
            }
            return postingsMatrix;
        }

        private Stopwatch Time()
        {
            var timer = new Stopwatch();
            timer.Start();
            return timer;
        }
    }
}