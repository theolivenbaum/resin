using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    public class Collector : IDisposable
    {
        private readonly string _directory;
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly IndexInfo _ix;
        private readonly ConcurrentDictionary<string, TrieStreamReader> _readers;
        private readonly TermDocumentMatrix _termDocMatrix;

        public Collector(string directory, IndexInfo ix, TermDocumentMatrix termDocMatrix)
        {
            _directory = directory;
            _ix = ix;
            _readers = new ConcurrentDictionary<string, TrieStreamReader>();
            _termDocMatrix = termDocMatrix;
        }

        public IEnumerable<DocumentScore> Collect(QueryContext queryContext, int page, int size, IScoringScheme scorer)
        {
            Expand(queryContext);
            Scan(queryContext, scorer);
            var scored = queryContext.Resolve().Values.OrderByDescending(s => s.Score).ToList();
            return scored;
        }

        private Trie GetTrie(string field)
        {
            TrieStreamReader reader;
            if (!_readers.TryGetValue(field, out reader))
            {
                var timer = new Stopwatch();
                timer.Start();
                var fileName = Path.Combine(_directory, field.ToTrieContainerId() + ".tc");
                var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                reader = new TrieStreamReader(fs);
                _readers.AddOrUpdate(field, reader, (s, streamReader) => streamReader);
                Log.DebugFormat("opened {0} in {1}", fileName, timer.Elapsed);
            }
            return reader.Reset();
        }

        private void Scan(QueryContext queryContext, IScoringScheme scorer)
        {
            queryContext.Result = GetScoredResult(queryContext, scorer).ToDictionary(x => x.DocId, y => y);
            foreach (var child in queryContext.Children)
            {
                Scan(child, scorer);
            }
        }

        private IEnumerable<DocumentScore> GetScoredResult(QueryTerm queryTerm, IScoringScheme scoringScheme)
        {
            var trie = GetTrie(queryTerm.Field);

            if (_ix == null) yield break;

            var totalNumOfDocs = _ix.DocumentCount.DocCount[queryTerm.Field];
            if (trie.HasWord(queryTerm.Value))
            {
                var weights = _termDocMatrix.Weights[new Term(queryTerm.Field, queryTerm.Value)];
                var scorer = scoringScheme.CreateScorer(totalNumOfDocs, weights.Count);
                foreach (var weight in weights)
                {
                    var hit = new DocumentScore(weight.DocumentId, weight.Weight, totalNumOfDocs);
                    scorer.Score(hit);
                    yield return hit;
                }
            }
        }

        private void Expand(QueryContext queryContext)
        {
            if (queryContext == null) throw new ArgumentNullException("queryContext");
            if (queryContext.Fuzzy || queryContext.Prefix)
            {
                var timer = new Stopwatch();
                timer.Start();

                var trie = GetTrie(queryContext.Field);

                IList<QueryContext> expanded = null;

                if (queryContext.Fuzzy)
                {
                    expanded = trie.Similar(queryContext.Value, queryContext.Edits).Select(token => new QueryContext(queryContext.Field, token)).ToList();
                }
                else if (queryContext.Prefix)
                {
                    expanded = trie.Prefixed(queryContext.Value).Select(token => new QueryContext(queryContext.Field, token)).ToList();
                }

                if (expanded != null)
                {
                    foreach (var t in expanded.Where(e=>e.Value != queryContext.Value))
                    {
                        queryContext.Children.Add(t);
                    }
                }

                queryContext.Prefix = false;
                queryContext.Fuzzy = false;

                Log.DebugFormat("expanded {0} in {1}", queryContext, timer.Elapsed);
            }
            foreach (var child in queryContext.Children)
            {
                Expand(child);
            }
        }

        public void Dispose()
        {
            foreach (var trie in _readers.Values)
            {
                trie.Dispose();
            }
        }
    }
}