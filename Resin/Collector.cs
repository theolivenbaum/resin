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
        private readonly Dictionary<string, Trie> _trieFiles;
        private readonly Dictionary<string, FieldFile> _fieldFiles;
        private readonly FixFile _fix;

        public Collector(string directory, FixFile fix, Dictionary<string, FieldFile> fieldFiles, Dictionary<string, Trie> trieFiles)
        {
            _directory = directory;
            _fix = fix;
            _fieldFiles = fieldFiles;
            _trieFiles = trieFiles;
        }

        public IEnumerable<DocumentScore> Collect(QueryContext queryContext, int page, int size)
        {
            Expand(queryContext);
            Scan(queryContext);
            var scored = queryContext.Resolve().Values.OrderByDescending(s => s.Score).ToList();
            return scored;
        }

        private Trie GetTrieFile(string field)
        {
            var fileId = _fix.FieldToFileId[field];
            var fileName = Path.Combine(_directory, fileId + ".f.tri");
            Trie file;
            if (!_trieFiles.TryGetValue(fileId, out file))
            {
                file = Trie.Load(fileName);
                _trieFiles[fileId] = file;
            }
            return file;
        }

        private FieldFile GetFieldFile(string field)
        {
            if (!_fix.FieldToFileId.ContainsKey(field)) return null;

            var fileId = _fix.FieldToFileId[field];
            var fileName = Path.Combine(_directory, fileId + ".f");
            FieldFile file;
            if (!_fieldFiles.TryGetValue(fileId, out file))
            {
                file = FieldFile.Load(fileName);
                _fieldFiles[fileId] = file;
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
            var fieldFile = GetFieldFile(term.Field);
            if (fieldFile == null) yield break;
            var docsInCorpus = fieldFile.NumDocs();
            Dictionary<string, int> postings;
            if (fieldFile.TryGetValue(term.Token, out postings))
            {
                var scorer = new Tfidf(docsInCorpus, postings.Count);
                foreach (var posting in postings)
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
                var trie = GetTrieFile(queryContext.Field);

                IList<QueryContext> expanded = null;

                if (queryContext.Fuzzy)
                {
                    expanded = trie.Similar(queryContext.Token, queryContext.Edits).Select(token => new QueryContext(queryContext.Field, token)).ToList();
                }
                else if (queryContext.Prefix)
                {
                    expanded = trie.Prefixed(queryContext.Token).Select(token => new QueryContext(queryContext.Field, token)).ToList();
                }

                if (expanded != null)
                {
                    var tokenSuffix = queryContext.Prefix ? "*" : queryContext.Fuzzy ? "~" : string.Empty;
                    Log.DebugFormat("{0}:{1}{2} expanded to {3}", queryContext.Field, queryContext.Token, tokenSuffix, string.Join(" ", expanded.Select(q => q.ToString())));
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