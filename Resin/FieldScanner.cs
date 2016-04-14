using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO;

namespace Resin
{
    public class FieldScanner
    {
        private readonly string _directory;

        // field/files
        private readonly IDictionary<string, IList<string>> _fieldIndex;

        // field/reader
        private readonly IDictionary<string, FieldReader> _readerCache;

        private static readonly object Sync = new object();
        private static readonly ILog Log = LogManager.GetLogger(typeof(FieldScanner));

        public FieldScanner(string directory, IDictionary<string, IList<string>> fieldIndex)
        {
            _readerCache = new Dictionary<string, FieldReader>();
            _directory = directory;
            _fieldIndex = fieldIndex;
        }

        public static FieldScanner MergeLoad(string directory)
        {
            var ixIds = Directory.GetFiles(directory, "*.ix")
                .Where(f => Path.GetExtension(f) != ".tmp")
                .Select(f => int.Parse(Path.GetFileNameWithoutExtension(f) ?? "-1"))
                .OrderBy(i => i).ToList();

            var fieldIndex = new Dictionary<string, IList<string>>();
            foreach (var ixFileName in ixIds.Select(id => Path.Combine(directory, id + ".ix")))
            {
                var ix = IxFile.Load(Path.Combine(directory, ixFileName));
                var fix = FixFile.Load(Path.Combine(directory, ix.FixFileName));
                foreach (var field in fix.FieldIndex)
                {
                    IList<string> files;
                    if (fieldIndex.TryGetValue(field.Key, out files))
                    {
                        files.Add(field.Value);
                    }
                    else
                    {
                        fieldIndex.Add(field.Key, new List<string> { field.Value });
                    }
                }
            }
            return new FieldScanner(directory, fieldIndex);
        }

        public IEnumerable<DocumentScore> GetDocIds(Term term)
        {
            IList<string> fieldFileIds;
            if (_fieldIndex.TryGetValue(term.Field, out fieldFileIds))
            {
                var reader = GetReader(term.Field);
                if (reader != null)
                {
                    return ExactMatch(term, reader);
                }
            }
            return Enumerable.Empty<DocumentScore>();
        }

        public void Expand(Query query)
        {
            IList<Query> expanded = null;
            
            if (query.Fuzzy)
            {
                expanded = GetReader(query.Field).GetSimilar(query.Token, query.Edits).Select(token => new Query(query.Field, token)).ToList();
            }
            else if (query.Prefix)
            {
                expanded = GetReader(query.Field).GetTokens(query.Token).Select(token => new Query(query.Field, token)).ToList();
            }

            if (expanded != null)
            {
                var tokenSuffix = query.Prefix ? "*" : query.Fuzzy ? "~" : string.Empty;
                Log.InfoFormat("{0}:{1}{2} expanded to {3}", query.Field, query.Token, tokenSuffix, string.Join(" ", expanded.Select(q=>q.ToString())));
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

        private IEnumerable<DocumentScore> ExactMatch(Term term, FieldReader reader)
        {
            var postings = reader.GetPostings(term.Token);
            if (postings != null)
            {
                foreach (var doc in postings)
                {
                    yield return new DocumentScore(doc.Key, doc.Value);
                }
            }
        }

        public FieldReader GetReader(string field)
        {
            FieldReader reader;
            if (!_readerCache.TryGetValue(field, out reader))
            {
                lock (Sync)
                {
                    if (!_readerCache.TryGetValue(field, out reader))
                    {
                        IList<string> files;
                        if (_fieldIndex.TryGetValue(field, out files))
                        {
                            foreach (var file in files)
                            {
                                var timer = new Stopwatch();
                                timer.Start();
                                var r = FieldReader.Load(Path.Combine(_directory, file + ".f"));
                                Log.InfoFormat("{0} reader loaded in {1}", field, timer.Elapsed);
                                if (reader == null)
                                {
                                    reader = r;
                                }
                                else
                                {
                                    reader.Merge(r);
                                }
                            }
                            _readerCache.Add(field, reader);
                            return reader;
                        }
                        return null;
                    }
                }
                
            }
            return reader;
        }

        public IEnumerable<string> GetAllTokensFromTrie(string field)
        {
            var reader = GetReader(field);
            return reader == null ? Enumerable.Empty<string>() : reader.GetAllTokensFromTrie();
        }

        public IEnumerable<TokenInfo> GetAllTokens(string field)
        {
            var reader = GetReader(field);
            return reader == null ? Enumerable.Empty<TokenInfo>() : reader.GetAllTokens();
        }

        public int DocsInCorpus(string field)
        {
            var reader = GetReader(field);
            if (reader != null)
            {
                return reader.DocCount;
            }
            return 0;
        }
    }
}