using System.Collections.Generic;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    public class Collector
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly Dictionary<string, Trie> _triesByFileId;
        private readonly Dictionary<string, FieldFile> _fieldFileByFileId;
        private readonly Dictionary<string, List<string>> _fileIdsByField;

        public Collector(IxFile ix, string directory)
        {
            _fieldFileByFileId = new Dictionary<string, FieldFile>();
            _triesByFileId = new Dictionary<string, Trie>();
            _fileIdsByField = new Dictionary<string, List<string>>();

            var fix = FixFile.Load(Path.Combine(directory, ix.FixFileName));
            foreach (var field in fix.FieldToFileId)
            {
                _fileIdsByField.Add(field.Key, new List<string> { field.Value });
                _fieldFileByFileId[field.Value] = FieldFile.Load(Path.Combine(directory, field.Value + ".f"));
                _triesByFileId[field.Key] = Trie.Load(Path.Combine(directory, field.Value + ".f.tri"));
            }
        }

        public Collector(Dictionary<string, List<string>> fileIdsByField, Dictionary<string, FieldFile> fieldFileByFileId, Dictionary<string, Trie> triesByFileId)
        {
            _fieldFileByFileId = fieldFileByFileId;
            _triesByFileId = triesByFileId;
            _fileIdsByField = fileIdsByField;
        }

        public IEnumerable<DocumentScore> Collect(QueryContext queryContext, int page, int size)
        {
            Expand(queryContext);
            Scan(queryContext);
            var scored = queryContext.Resolve().Values.OrderByDescending(s => s.Score).ToList();
            return scored;
        }

        private IEnumerable<FieldFile> GetFieldFiles(string field)
        {
            List<string> fileIds;
            if (_fileIdsByField.TryGetValue(field, out fileIds))
            {
                foreach (var fileId in fileIds)
                {
                    yield return _fieldFileByFileId[fileId];
                }
            }
        }

        private IEnumerable<Trie> GetTries(string field)
        {
            List<string> fileIds;
            if (_fileIdsByField.TryGetValue(field, out fileIds))
            {
                foreach (var fileId in fileIds)
                {
                    yield return _triesByFileId[fileId];
                }
            }
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
            var fieldFiles = GetFieldFiles(term.Field).ToList();
            var postings = new Dictionary<string, int>();
            int docsInCorpus = 0;
            foreach (var fieldFile in fieldFiles)
            {
                Dictionary<string, int> ps;
                if (fieldFile.TryGetValue(term.Value, out ps))
                {
                    foreach (var p in ps)
                    {
                        postings[p.Key] = p.Value; //overwrite the freq with the most recent value
                    }
                }
                docsInCorpus = fieldFile.NumDocs();
            }
            var scorer = new Tfidf(docsInCorpus, postings.Count);
            foreach (var posting in postings)
            {
                var hit = new DocumentScore(posting.Key, posting.Value);
                scorer.Score(hit);
                yield return hit;
            }     
        }

        private void Expand(QueryContext queryContext)
        {
            if (queryContext.Fuzzy || queryContext.Prefix)
            {
                var ts = GetTries(queryContext.Field).ToList();
                var words = new List<string>();
                foreach (var t in ts)
                {
                    if (queryContext.Fuzzy)
                    {
                        var mightMean = t.Similar(queryContext.Value, queryContext.Edits);
                        words.AddRange(mightMean);
                    }
                    else if (queryContext.Prefix)
                    {
                        var mightMean = t.Prefixed(queryContext.Value);
                        words.AddRange(mightMean);
                    }
                }
                var expanded = words.Select(token => new QueryContext(queryContext.Field, token)).ToList();
                var tokenSuffix = queryContext.Prefix ? "*" : queryContext.Fuzzy ? "~" : string.Empty;
                Log.DebugFormat("{0}:{1}{2} expanded to {3}", queryContext.Field, queryContext.Value, tokenSuffix, string.Join(" ", expanded.Select(q => q.ToString())));
                foreach (var t in expanded)
                {
                    queryContext.Children.Add(t);
                }
            }
        }
    }
}