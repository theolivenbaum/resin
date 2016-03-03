using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class DocumentScanner
    {
        private readonly string _directory;
        private readonly IDictionary<string, IList<int>> _fieldIndex;

        public string Dir { get { return _directory; } }

        public DocumentScanner(string directory)
        {
            _directory = directory;
            var indexFileName = Path.Combine(directory, "field.idx");
            using (var fs = File.OpenRead(indexFileName))
            {
                _fieldIndex = Serializer.Deserialize<Dictionary<string, IList<int>>>(fs);
            }
        }

        public IList<int> GetDocIds(string field, string value)
        {
            IList<int> fieldIds;
            if (_fieldIndex.TryGetValue(field, out fieldIds))
            {
                var fileNames = fieldIds.Select(id => Path.Combine(_directory, id + ".fld")).ToArray();
                var fr = FieldReader.LoadAndMerge(fileNames);
                var positions = fr.GetDocPosition(value);
                if (positions != null)
                {
                    var ordered = positions.OrderByDescending(d => d.Value.Count).Select(d => d.Key).ToList();
                    return ordered;    
                }                
            }
            return Enumerable.Empty<int>().ToList();
        }

    }
}