using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class DocumentFile : IDisposable
    {
        private readonly string _dir;
        private readonly IDictionary<int, IDictionary<string, IList<string>>> _docs;

        public DocumentFile(string dir)
        {
            _dir = dir;
            _docs = new Dictionary<int, IDictionary<string, IList<string>>>();
        }

        public void Write(int docId, string fieldName, string fieldValue)
        {
            IDictionary<string, IList<string>> doc;
            if (!_docs.TryGetValue(docId, out doc))
            {
                doc = new Dictionary<string, IList<string>>();
                _docs.Add(docId, doc);
            }
            IList<string> values;
            if (!doc.TryGetValue(fieldName, out values))
            {
                values = new List<string> { fieldValue };
                doc.Add(fieldName, values);
            }
            else
            {
                values.Add(fieldValue);
            }
        }
        private void Flush()
        {
            if (_docs.Count == 0) return;

            var ixFileName = Path.Combine(_dir, "d.ix");
            var id = Directory.GetFiles(_dir, "*.d").Length;
            var fileName = Path.Combine(_dir, id + ".d");
            if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
            File.WriteAllText(fileName, "");
            
            using (var fs = File.Create(fileName))
            {
                Serializer.Serialize(fs, _docs);
            }

            IDictionary<int, int> docIdToFileIndex;
            if (File.Exists(ixFileName))
            {
                using (var file = File.OpenRead(ixFileName))
                {
                    docIdToFileIndex = Serializer.Deserialize<IDictionary<int, int>>(file);
                }
            }
            else
            {
                docIdToFileIndex = new Dictionary<int, int>();
            }
            foreach (var docId in _docs.Keys)
            {
                docIdToFileIndex[docId] = id;
            }
            _docs.Clear();
            using (var fs = File.Create(ixFileName))
            {
                Serializer.Serialize(fs, docIdToFileIndex);
            }
        }

        public void Dispose()
        {
            Flush();
        }
    }
}