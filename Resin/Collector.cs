using System.Collections.Generic;
using System.Globalization;
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
        private readonly IxFile _ix;

        public Collector(string directory, IxFile ix, IDictionary<string, Trie> trieFiles)
        {
            _directory = directory;
            _trieFiles = trieFiles;
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
            if (_ix.Fields.ContainsKey(field))
            {
                var id = field.ToHash().ToString(CultureInfo.InvariantCulture);
                Trie file;
                if (!_trieFiles.TryGetValue(id, out file))
                {
                    var fileName = Path.Combine(_directory, id + ".tr");
                    file = Trie.Load(fileName);
                    _trieFiles.Add(id, file);
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
            var fieldTokenId = string.Format("{0}.{1}", field, token);
            var fileId = fieldTokenId.ToHash().ToString(CultureInfo.InvariantCulture);
            var fileName = Path.Combine(_directory, fileId + ".po");
            return PostingsFile.Load(fileName);
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