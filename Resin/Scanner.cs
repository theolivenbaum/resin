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

        public IList<int> GetDocIds(string field, string value)
        {
            int fieldId;
            if (_fieldIndex.TryGetValue(field, out fieldId))
            {
                var reader = GetReader(field);
                if (reader != null)
                {
                    var positions = reader.GetDocPosition(value);
                    if (positions != null)
                    {
                        var ordered = positions.OrderByDescending(d => d.Value.Count).Select(d => d.Key).ToList();
                        return ordered;
                    }
                }
            }
            return Enumerable.Empty<int>().ToList();
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