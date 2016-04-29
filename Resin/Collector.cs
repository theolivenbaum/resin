using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<string, PostingsContainerFile> _postingsCache;
        private readonly IxFile _ix;

        public Collector(string directory, IxFile ix, ConcurrentDictionary<string, LazyTrie> trieFiles, ConcurrentDictionary<string, PostingsContainerFile> postingsCache)
        {
            _directory = directory;
            _trieFiles = trieFiles;
            _postingsCache = postingsCache;
            _ix = ix;
        }

        public IEnumerable<DocumentScore> Collect(QueryContext queryContext, int page, int size)
        {
            Expand(queryContext);
            Scan(queryContext);
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

        private void Scan(QueryContext queryContext)
        {
            queryContext.Result = GetScoredResult(queryContext).ToDictionary(x => x.DocId, y => y);
            foreach (var child in queryContext.Children)
            {
                Scan(child);
            }
        }

        private IEnumerable<DocumentScore> GetScoredResult(Term term)
        {
            var trie = GetTrie(term.Field);
            if (trie == null) yield break;
            var docsInCorpus = _ix.Fields[term.Field].Count;
            if (trie.ContainsToken(term.Value))
            {
                var postingsFile = GetPostingsFile(term.Field, term.Value);
                var scorer = new Tfidf(docsInCorpus, postingsFile.Postings.Count);
                foreach (var posting in postingsFile.Postings)
                {
                    var hit = new DocumentScore(posting.Key, posting.Value);
                    scorer.Score(hit);
                    yield return hit;
                }
            }
        }

        private PostingsFile GetPostingsFile(string field, string token)
        {
            var fileId = token.ToPostingHash();
            PostingsContainerFile container;
            if (!_postingsCache.TryGetValue(fileId, out container))
            {
                var fileName = Path.Combine(_directory, fileId + ".pl");
                container = PostingsContainerFile.Load(fileName);
                _postingsCache[fileId] = container;
            }
            var fieldTokenId = string.Format("{0}.{1}", field, token);
            return container.Files[fieldTokenId];
        }

        private void Expand(QueryContext queryContext)
        {
            if (queryContext == null) throw new ArgumentNullException("queryContext");
            if (queryContext.Fuzzy || queryContext.Prefix)
            {
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
                    var tokenSuffix = queryContext.Prefix ? "*" : queryContext.Fuzzy ? "~" : string.Empty;
                    Log.DebugFormat("{0}:{1}{2} expanded to {3}", queryContext.Field, queryContext.Value, tokenSuffix, string.Join(" ", expanded.Select(q => q.ToString())));
                    foreach (var t in expanded)
                    {
                        queryContext.Children.Add(t);
                    }
                }

                queryContext.Prefix = false;
                queryContext.Fuzzy = false;

                foreach (var child in queryContext.Children)
                {
                    Expand(child);
                }
            }
        }
    }
}