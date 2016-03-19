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
                    var postings = reader.GetPostings(term.Token);
                    if (postings != null)
                    {
                        foreach (var posting in postings)
                        {
                            yield return new DocumentScore {DocId = posting.Key, Value = posting.Value.Count};
                        }
                    }
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

        public ICollection<string> GetAllTokens(string field)
        {
            int fieldId;
            if (_fieldIndex.TryGetValue(field, out fieldId))
            {
                var f = FieldReader.Load(Path.Combine(_directory, fieldId + ".fld"));
                return f.GetAllTokens();
            }
            return Enumerable.Empty<string>().ToList();
        } 
    }
}