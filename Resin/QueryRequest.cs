using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    public class QueryRequest
    {
        private readonly string _directory;
        private static readonly ILog Log = LogManager.GetLogger(typeof(QueryRequest));
        private readonly IDictionary<string, Trie> _trieFiles;
        private readonly IDictionary<string, FieldFile> _fieldFiles;
        private readonly FixFile _fix;

        public QueryRequest(string directory, string fixFileName, Dictionary<string, FieldFile> fieldFiles)
        {
            _directory = directory;
            _fix = FixFile.Load(Path.Combine(directory, fixFileName));
            _fieldFiles = fieldFiles;
            _trieFiles = new Dictionary<string, Trie>();
        }

        public IEnumerable<DocumentScore> GetResult(Query query, int page, int size, bool returnTrace)
        {
            Expand(query);
            Scan(query);
            var scored = query.Resolve().Values.OrderByDescending(s => s.Score).ToList();
            return scored;
        }

        private Trie GetTrieFile(string field)
        {
            var fileName = Path.Combine(_directory,_fix.FieldIndex[field] + ".tri");
            Trie file;
            if (!_trieFiles.TryGetValue(fileName, out file))
            {
                file = Trie.Load(fileName);
                _trieFiles[fileName] = file;
            }
            return file;
        }

        private FieldFile GetFieldFile(string field)
        {
            if (!_fix.FieldIndex.ContainsKey(field)) return null;

            var fileName = Path.Combine(_directory, _fix.FieldIndex[field] + ".f");
            FieldFile file;
            if (!_fieldFiles.TryGetValue(fileName, out file))
            {
                file = FieldFile.Load(fileName);
                _fieldFiles[fileName] = file;
            }
            return file;
        }

        private void Scan(Query query)
        {
            query.Result = GetScoredResult(query).ToDictionary(x => x.DocId, y => y);
            foreach (var child in query.Children)
            {
                Scan(child);
            }
        }

        private IEnumerable<DocumentScore> GetScoredResult(Term term)
        {
            var fieldFile = GetFieldFile(term.Field);
            if (fieldFile == null) yield break;
            var docsInCorpus = fieldFile.Terms.Values.SelectMany(x => x.Keys).ToList().Distinct().Count();
            Dictionary<string, int> postings;
            if (fieldFile.Terms.TryGetValue(term.Token, out postings))
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

        private void Expand(Query query)
        {
            if (query.Fuzzy || query.Prefix)
            {
                var trie = GetTrieFile(query.Field);

                IList<Query> expanded = null;

                if (query.Fuzzy)
                {
                    expanded = trie.Similar(query.Token, query.Edits).Select(token => new Query(query.Field, token)).ToList();
                }
                else if (query.Prefix)
                {
                    expanded = trie.Prefixed(query.Token).Select(token => new Query(query.Field, token)).ToList();
                }

                if (expanded != null)
                {
                    var tokenSuffix = query.Prefix ? "*" : query.Fuzzy ? "~" : string.Empty;
                    Log.InfoFormat("{0}:{1}{2} expanded to {3}", query.Field, query.Token, tokenSuffix, string.Join(" ", expanded.Select(q => q.ToString())));
                    foreach (var t in expanded)
                    {
                        query.Children.Add(t);
                    }
                }

                query.Prefix = false;
                query.Fuzzy = false;

                foreach (var child in query.Children)
                {
                    Expand(child);
                }   
            }
        }
    }
}