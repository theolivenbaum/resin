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
        private readonly IDictionary<string, Trie> _trieFiles;
        private readonly FixFile _fix;

        public Collector(string directory, FixFile fix, IDictionary<string, Trie> trieFiles)
        {
            _directory = directory;
            _trieFiles = trieFiles;
            _fix = fix;
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
            if (_fix.Fields.ContainsKey(field))
            {
                var fileId = _fix.Fields[field];
                Trie file;
                if (!_trieFiles.TryGetValue(fileId, out file))
                {
                    var fileName = Path.Combine(_directory, fileId + ".tr");
                    file = Trie.Load(fileName);
                    _trieFiles.Add(fileId, file);
                }
                return file;
            }
            return null;
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
            var docsInCorpus = 10000;
            if (trie.ContainsToken(term.Value))
            {
                var fileId = string.Format("{0}.{1}", _fix.Fields[term.Field], term.Value.ToNumericalString());
                var fileName = Path.Combine(_directory, fileId + ".po");
                var postingsFile = PostingsFile.Load(fileName);
                var scorer = new Tfidf(docsInCorpus, postingsFile.Postings.Count);
                foreach (var posting in postingsFile.Postings)
                {
                    var hit = new DocumentScore(posting.Key, posting.Value);
                    scorer.Score(hit);
                    yield return hit;
                }
            }
        }

        private void Expand(QueryContext queryContext)
        {
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