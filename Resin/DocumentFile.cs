using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class DocumentFile : IDisposable
    {
        private readonly string _fileName;
        private readonly IDictionary<string, IList<string>> _doc;
 
        public DocumentFile(string fileName, bool overwrite)
        {
            _fileName = fileName;
            if (!overwrite && File.Exists(fileName))
            {
                using (var file = File.OpenRead(fileName))
                {
                    _doc = Serializer.Deserialize<Dictionary<string, IList<string>>>(file);
                }
            }
            else
            {
                _doc = new Dictionary<string, IList<string>>();
            }
        }

        public void Write(string fieldName, string fieldValue)
        {
            IList<string> values;
            if (!_doc.TryGetValue(fieldName, out values))
            {
                values = new List<string> {fieldValue};
                _doc.Add(fieldName, values);
            }
            else
            {
                values.Add(fieldValue);
            }
        }

        private void Flush()
        {
            var dir = Path.GetDirectoryName(_fileName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            using (var fs = File.Create(_fileName))
            {
                Serializer.Serialize(fs, _doc);
            }
        }

        public void Dispose()
        {
            Flush();
        }
    }

    public class MultiDocumentFile : IDisposable
    {
        private readonly string _dir;
        private readonly IDictionary<int, IDictionary<string, IList<string>>> _docs;

        public MultiDocumentFile(string dir)
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
            var idxFileName = Path.Combine(_dir, "d.ix");

            if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);

            var id = Directory.GetFiles(_dir, "*.d").Length;
            var fileName = Path.Combine(_dir, id + ".d");
            using (var fs = File.Create(fileName))
            {
                Serializer.Serialize(fs, _docs);
            }
            IDictionary<int, int> docIdToFileIndex;
            if (File.Exists(idxFileName))
            {
                using (var file = File.OpenRead(idxFileName))
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
            using (var fs = File.Create(idxFileName))
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