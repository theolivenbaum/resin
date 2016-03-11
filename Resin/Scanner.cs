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

        public string Dir { get { return _directory; } }

        public Scanner(string directory)
        {
            _directory = directory;
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
                var fr = FieldReader.Load(Path.Combine(_directory, fieldId + ".fld"));
                var positions = fr.GetDocPosition(value);
                if (positions != null)
                {
                    var ordered = positions.OrderByDescending(d => d.Value.Count).Select(d => d.Key).ToList();
                    return ordered;    
                }                
            }
            return Enumerable.Empty<int>().ToList();
        }

        public ICollection<string> GetAllTerms(string field)
        {
            int fieldId;
            if (_fieldIndex.TryGetValue(field, out fieldId))
            {
                var f = FieldReader.Load(Path.Combine(_directory, fieldId + ".fld"));
                return f.GetAllTerms();
            }
            return Enumerable.Empty<string>().ToList();
        } 
    }
}