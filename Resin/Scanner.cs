using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class Scanner
    {
        private readonly string _directory;
        private readonly IDictionary<string, int> _fieldIndex;
        private readonly IDictionary<string, FieldReader> _fieldReaders; 

        public string Dir { get { return _directory; } }

        public Scanner(string directory)
        {
            _directory = directory;
            _fieldReaders = new Dictionary<string, FieldReader>();

            var indexFileName = Path.Combine(directory, "fld.ix");
            using (var fs = File.OpenRead(indexFileName))
            {
                _fieldIndex = Serializer.Deserialize<Dictionary<string, int>>(fs);
            }
        }

        public IEnumerable<DocumentScore> GetDocIds(Term term)
        {
            int fieldId;
            if (_fieldIndex.TryGetValue(term.Field, out fieldId))
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
            int fieldId;
            if (_fieldIndex.TryGetValue(field, out fieldId))
            {
                FieldReader reader;
                if (!_fieldReaders.TryGetValue(field, out reader))
                {
                    reader = FieldReader.Load(Path.Combine(_directory, fieldId + ".fld"));
                    _fieldReaders.Add(field, reader);
                }
                return reader;
            }
            return null;
        }

        public IEnumerable<TokenInfo> GetAllTokens(string field)
        {
            int fieldId;
            if (_fieldIndex.TryGetValue(field, out fieldId))
            {
                var f = FieldReader.Load(Path.Combine(_directory, fieldId + ".fld"));
                return f.GetAllTokens();
            }
            return Enumerable.Empty<TokenInfo>().ToList();
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