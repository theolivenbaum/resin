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
    public class Collector
    {
        private readonly string _directory;
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly ConcurrentDictionary<string, LazyTrie> _trieFiles;
        private readonly ConcurrentDictionary<string, PostingsContainer> _postingsCache;
        private readonly IxInfo _ix;

        public Collector(string directory, IxInfo ix, ConcurrentDictionary<string, LazyTrie> trieFiles, ConcurrentDictionary<string, PostingsContainer> postingsCache)
        {
            _directory = directory;
            _trieFiles = trieFiles;
            _postingsCache = postingsCache;
            _ix = ix;
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
            LazyTrie file;
            if (!_trieFiles.TryGetValue(field, out file))
            {
                file = new LazyTrie(_directory, field);
                _trieFiles[field] = file;
            }
            return file;
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
            if (trie.ContainsToken(term.Value))
            {
                var termData = GetPostingsFile(term.Field, term.Value);
                var scorer = scoringScheme.CreateScorer(totalNumOfDocs, termData.Postings.Count);
                foreach (var posting in termData.Postings)
                {
                    var hit = new DocumentScore(posting.Key, posting.Value, totalNumOfDocs);
                    scorer.Score(hit);
                    //if (hit.Score > 4d) Log.Info(hit);
                    yield return hit;
                }
            }
        }

        private PostingsFile GetPostingsFile(string field, string token)
        {
            var bucketId = field.ToPostingsBucket(token[0]);
            PostingsContainer container;
            if (!_postingsCache.TryGetValue(bucketId, out container))
            {
                var fileName = Path.Combine(_directory, bucketId + ".pl");
                container = PostingsContainer.Load(fileName);
                _postingsCache[bucketId] = container;
            }
            var fieldTokenId = string.Format("{0}.{1}", field, token);
            return container.Files[fieldTokenId];
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
                    foreach (var t in expanded)
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
    }
}