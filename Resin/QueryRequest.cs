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
        private readonly IDictionary<string, DocFile> _docFiles;
        private readonly FixFile _fix;
        private readonly DixFile _dix;

        public QueryRequest(string directory)
        {
            _directory = directory;
            var ix = IxFile.Load(Path.Combine(directory, "0.ix"));
            _fix = FixFile.Load(Path.Combine(directory, ix.FixFileName));
            _dix = DixFile.Load(Path.Combine(directory, ix.DixFileName));
            _docFiles = new Dictionary<string, DocFile>();
            _fieldFiles = new Dictionary<string, FieldFile>();
            _trieFiles = new Dictionary<string, Trie>();
        }

        public Result GetResult(Query query, int page, int size, bool returnTrace)
        {
            Expand(query);
            Scan(query);
            var scored = query.Resolve().Values.OrderByDescending(s => s.Score).ToList();
            var skip = page * size;
            var paged = scored.Skip(skip).Take(size).ToDictionary(x => x.DocId, x => x);
            var trace = returnTrace ? paged.ToDictionary(ds => ds.Key, ds => ds.Value.Trace.ToString() + paged[ds.Key].Score) : null;
            var docs = paged.Values.Select(s => GetDoc(s.DocId)).ToList();
            return new Result { Docs = docs, Total = scored.Count, Trace = trace };
        }

        private IDictionary<string, string> GetDoc(string docId)
        {
            var fileName = Path.Combine(_directory, _dix.DocIdToFileIndex[docId] + ".d");
            DocFile file;
            if (!_docFiles.TryGetValue(fileName, out file))
            {
                file = DocFile.Load(fileName);
                _docFiles[fileName] = file;
            }
            return file.Docs[docId].Fields;
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