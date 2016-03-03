using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class DocumentWriter : IDisposable
    {
        private readonly string _fileName;
        private readonly IDictionary<string, IList<string>> _doc;
 
        public DocumentWriter(string fileName)
        {
            _fileName = fileName;
            if (File.Exists(fileName))
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

        public void Dispose()
        {
            using (var fs = File.Create(_fileName))
            {
                Serializer.Serialize(fs, _doc);
            }
        }
    }
}