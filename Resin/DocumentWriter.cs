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

        // docid/fields/value
        private readonly IDictionary<int, IDictionary<string, string>> _docs;

        public DocumentWriter(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            _dir = dir;
            _docs = new Dictionary<int, IDictionary<string, string>>();
        }

        public void Write(int docId, string field, string value)
        {
            IDictionary<string, string> doc;
            if (!_docs.TryGetValue(docId, out doc))
            {
                doc = new Dictionary<string, string>();
                _docs.Add(docId, doc);
            }
            doc[field] = value;
        }

        public void Flush(string docixFileName)
        {

            if (_flushed || _docs.Count == 0) return;

            // docid/file
            var docIdToFileIndex = new Dictionary<int, string>();
            var batches = _docs.IntoBatches(1000).ToList();
            foreach (var batch in batches)
            {
                var docs = batch.ToList();
                var fileId = Path.GetRandomFileName();
                var fileName = Path.Combine(_dir, fileId + ".d");
                using (var fs = File.Create(fileName))
                {
                    Serializer.Serialize(fs, docs.ToDictionary(x => x.Key, y => y.Value));
                }
                foreach (var docId in docs)
                {
                    docIdToFileIndex[docId.Key] = fileId;
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