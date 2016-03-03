using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class IndexReader
    {
        private readonly DocumentScanner _scanner;

        public IndexReader(DocumentScanner scanner)
        {
            _scanner = scanner;
        }

        public IEnumerable<IDictionary<string, IList<string>>> GetDocuments(string field, string value)
        {
            //var docs = new List<IDictionary<string, IList<string>>>(); 
            foreach(var docId in _scanner.GetDocIds(field, value))
            {
                var doc = new Dictionary<string, IList<string>>();
                var docIndex = File.ReadAllLines(Path.Combine(_scanner.Dir, docId + ".doc"));
                foreach (var fileName in docIndex)
                {
                    var file = File.ReadAllText(fileName).Split(':');
                    var fieldName = file[0];
                    var fieldValue = file[1];
                    IList<string> values;
                    if (doc.TryGetValue(fieldName, out values))
                    {
                        values.Add(fieldValue);
                    }
                    else
                    {
                        doc.Add(fieldName, new List<string>{fieldValue});
                    }
                }
                yield return doc;
            }
        }
    }
}