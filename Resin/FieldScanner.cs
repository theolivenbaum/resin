using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class FieldScanner
    {
        private readonly string _directory;

        // field/files
        private readonly IDictionary<string, IList<string>> _fieldIndex;

        // field/reader
        private readonly IDictionary<string, FieldReader> _readerCache; 

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
                .Select(f => int.Parse(Path.GetFileNameWithoutExtension(f)))
                .OrderBy(i => i).ToList();

            var fieldIndex = new Dictionary<string, IList<string>>();
            foreach (var ixFileName in ixIds.Select(id => Path.Combine(directory, id + ".ix")))
            {
                Index ix;
                using (var fs = File.OpenRead(ixFileName))
                {
                    ix = Serializer.Deserialize<Index>(fs);
                }
                IDictionary<string, string> fix;
                using (var fs = File.OpenRead(ix.FixFileName))
                {
                    fix = Serializer.Deserialize<Dictionary<string, string>>(fs);
                }
                foreach (var field in fix)
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
                    if (term.Prefix)
                    {
                        return GetDocIdsByPrefix(term, reader);
                    }
                    if (term.Fuzzy)
                    {
                        return GetDocIdsFuzzy(term, reader);
                    }
                    return GetDocIdsExact(term, reader);
                }
            }
            return Enumerable.Empty<DocumentScore>();
        }

        private IEnumerable<DocumentScore> GetDocIdsFuzzy(Term term, FieldReader reader)
        {
            var terms = reader.GetSimilar(term.Token, term.Edits).Select(token => new Term { Field = term.Field, Token = token }).ToList();
            return terms.SelectMany(t => GetDocIdsExact(t, reader)).GroupBy(d => d.DocId).Select(g => g.OrderByDescending(x => x.TermFrequency).First());
        }

        private IEnumerable<DocumentScore> GetDocIdsByPrefix(Term term, FieldReader reader)
        {
            var terms = reader.GetTokens(term.Token).Select(token => new Term {Field = term.Field, Token = token}).ToList();
            return terms.SelectMany(t => GetDocIdsExact(t, reader)).GroupBy(d=>d.DocId).Select(g=>g.OrderByDescending(x=>x.TermFrequency).First());
        }

        private IEnumerable<DocumentScore> GetDocIdsExact(Term term, FieldReader reader)
        {
            var postings = reader.GetPostings(term.Token);
            if (postings != null)
            {
                foreach (var doc in postings)
                {
                    yield return new DocumentScore { DocId = doc.Key, TermFrequency = doc.Value };
                }
            }
        }

        private FieldReader GetReader(string field)
        {
            FieldReader reader;
            if (!_readerCache.TryGetValue(field, out reader))
            {
                IList<string> files;
                if (_fieldIndex.TryGetValue(field, out files))
                {
                    foreach (var file in files)
                    {
                        var r = FieldReader.Load(Path.Combine(_directory, file + ".f"));
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
            return reader;
        }

        public IEnumerable<TokenInfo> GetAllTokens(string field)
        {
            var reader = GetReader(field);
            return reader == null ? Enumerable.Empty<TokenInfo>() : reader.GetAllTokens();
        }

        public int DocCount(string field)
        {
            var reader = GetReader(field);
            if (reader != null)
            {
                return reader.DocCount;
            }
            return 0;
        }
    }

    public struct TokenInfo
    {
        public string Token;
        public int Count;
    }
}