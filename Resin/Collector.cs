using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<string, PostingsContainer> _postingContainers;
        private readonly IxInfo _ix;
        private readonly ConcurrentDictionary<string, Trie> _trieCache;
 
        public Collector(string directory, IxInfo ix, ConcurrentDictionary<string, PostingsContainer> postingContainers)
        {
            _directory = directory;
            _postingContainers = postingContainers;
            _ix = ix;
            _trieCache = new ConcurrentDictionary<string, Trie>();
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
            Trie trie;
            if (!_trieCache.TryGetValue(field, out trie))
            {
                trie = GetTrieFromIo(field);
                _trieCache.AddOrUpdate(field, trie, (s, trie1) => trie1);
            }
            return trie;
        }

        private Trie GetTrieFromIo(string field)
        {
            var timer = new Stopwatch();
            timer.Start();
            var fileName = Path.Combine(_directory, field.ToTrieContainerId() + ".tc");
            var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sr = new StreamReader(fs, Encoding.Unicode);
            var reader = new TrieStreamReader(sr);
            var trie = reader.Read();
            Log.DebugFormat("read {0} in {1}", fileName, timer.Elapsed);
            return trie;
        }

        private void Scan(QueryContext queryContext, IScoringScheme scorer)
        {
            queryContext.Result = GetScoredResult(queryContext, scorer).ToDictionary(x => x.DocId, y => y);
            foreach (var child in queryContext.Children)
            {
                Scan(child, scorer);
            }
        }

        private IEnumerable<DocumentScore> GetScoredResult(Term term, IScoringScheme scoringScheme)
        {
            var trie = GetTrie(term.Field);

            if (_ix == null) yield break;

            var totalNumOfDocs = _ix.DocCount[term.Field];
            if (trie.HasWord(term.Value))
            {
                var termData = GetPostingsFile(term.Field, term.Value);
                var scorer = scoringScheme.CreateScorer(totalNumOfDocs, termData.Postings.Count);
                foreach (var posting in termData.Postings)
                {
                    var hit = new DocumentScore(posting.Key, posting.Value, totalNumOfDocs);
                    scorer.Score(hit);
                    yield return hit;
                }
            }
        }

        private PostingsFile GetPostingsFile(string field, string token)
        {
            var containerId = field.ToPostingsContainerId();
            PostingsContainer container;
            if (!_postingContainers.TryGetValue(containerId, out container))
            {
                container = new PostingsContainer(_directory, containerId, eager:false);
                _postingContainers[containerId] = container;
            }
            return container.Get(token);
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
            foreach (var trie in _trieCache.Values)
            {
                trie.Dispose();
            }
        }
    }
}