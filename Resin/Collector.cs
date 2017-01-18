using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using Resin.IO;

namespace Resin
{
    public class Collector : IDisposable
    {
        private readonly string _directory;
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly IndexInfo _ix;
        private readonly IDictionary<Term, List<DocumentWeight>> _termCache;
        private readonly IList<TrieStreamReader> _readers;
 
        public Collector(string directory, IndexInfo ix)
        {
            _directory = directory;
            _ix = ix;
            _termCache = new Dictionary<Term, List<DocumentWeight>>();
            _readers = new List<TrieStreamReader>();
        }

        public IEnumerable<DocumentScore> Collect(QueryContext queryContext, int page, int size, IScoringScheme scorer)
        {
            Expand(queryContext);
            Scan(queryContext, scorer);
            var scored = queryContext.Resolve().Values.OrderByDescending(s => s.Score).ToList();
            return scored;
        }

        private IList<DocumentWeight> GetWeights(Term term)
        {
            List<DocumentWeight> weights;
            if (!_termCache.TryGetValue(term, out weights))
            {
                int[] posAndLen;
                if (_ix.Postings.TryGetValue(term, out posAndLen))
                {
                    var offset = posAndLen[0];
                    var length = posAndLen[1];
                    return ReadWeights(Path.Combine(_directory, "0.pos"), offset, length).ToList();
                }
            }
            return new List<DocumentWeight>();
        }

        private IEnumerable<DocumentWeight> ReadWeights(string fileName, int offset, int length)
        {
            //return File.ReadLines(fileName, Encoding.Unicode).Skip(offset).Take(length).Select(line =>
            //{
            //    var segs = line.Split(':');
            //    var documentId = segs[0];
            //    var weight = int.Parse(segs[1]);
            //    return new DocumentWeight(documentId, weight);
            //});
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fs, Encoding.Unicode))
            {
                var row = 0;
                while (row++ < offset)
                {
                    reader.ReadLine();
                }
                for (int i = 0; i < length; i++)
                {
                    var line = reader.ReadLine().Split(':');
                    var documentId = line[0];
                    var weight = int.Parse(line[1]);
                    yield return new DocumentWeight(documentId, weight);
                }
            }
        } 

        private TrieScanner GetTrie(string field)
        {
            var timer = new Stopwatch();
            timer.Start();
            var fileName = Path.Combine(_directory, field.ToTrieContainerId() + ".tc");
            var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var reader = new TrieStreamReader(fs);
            _readers.Add(reader);
            Log.DebugFormat("opened {0} in {1}", fileName, timer.Elapsed);
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
            if (trie == null) yield break;

            var totalNumOfDocs = _ix.DocumentCount.DocCount[queryTerm.Field];
            if (trie.HasWord(queryTerm.Value))
            {
                var weights = GetWeights(new Term(queryTerm.Field, queryTerm.Value));
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
                    foreach (var t in expanded.Where(e => e.Value != queryContext.Value))
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
            foreach (var r in _readers)
            {
                r.Dispose();
            }
        }
    }
}