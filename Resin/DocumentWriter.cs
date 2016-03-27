using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class DocumentWriter
    {
        private bool _flushed;
        private readonly string _dir;

        // docid/fields/values
        private readonly IDictionary<int, IDictionary<string, IList<string>>> _docs;

        public DocumentWriter(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            _dir = dir;
            _docs = new Dictionary<int, IDictionary<string, IList<string>>>();
        }

        public void Write(int docId, string field, string text)
        {
            IDictionary<string, IList<string>> doc;
            if (!_docs.TryGetValue(docId, out doc))
            {
                doc = new Dictionary<string, IList<string>>();
                _docs.Add(docId, doc);
            }
            IList<string> values;
            if (!doc.TryGetValue(field, out values))
            {
                values = new List<string> { text };
                doc.Add(field, values);
            }
            else
            {
                values.Add(text);
            }
        }

        public void Flush(string docixFileName)
        {

            if (_flushed || _docs.Count == 0) return;
 
            var docIdToFileIndex = new Dictionary<int, string>();
            var batches = _docs.IntoBatches(1000).ToList();
            foreach (var batch in batches)
            {
                var docs = batch.ToList();
                var fileName = Path.Combine(_dir, Path.GetRandomFileName() + ".d");
                using (var fs = File.Create(fileName))
                {
                    Serializer.Serialize(fs, docs.ToDictionary(x => x.Key, y => y.Value));
                }
                foreach (var docId in docs)
                {
                    docIdToFileIndex[docId.Key] = fileName;
                }
            }

            using (var fs = File.Create(docixFileName))
            {
                Serializer.Serialize(fs, docIdToFileIndex);
            }
            _docs.Clear();
            _flushed = true;
        }
    }
}