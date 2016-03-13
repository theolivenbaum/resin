using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class FieldFile : IDisposable
    {
        private readonly string _fileName;

        // terms/docids/positions
        private readonly IDictionary<string, IDictionary<int, IList<int>>> _terms;

        public FieldFile(string fileName)
        {
            _fileName = fileName;
            if (File.Exists(fileName))
            {
                using (var file = File.OpenRead(fileName))
                {
                    _terms = Serializer.Deserialize<Dictionary<string, IDictionary<int, IList<int>>>>(file);
                }
            }
            else
            {
                _terms = new Dictionary<string, IDictionary<int, IList<int>>>();
            }
        }

        public void Write(int docId, string termValue, int position)
        {
            IDictionary<int, IList<int>> docs;
            if (!_terms.TryGetValue(termValue, out docs))
            {
                docs = new Dictionary<int, IList<int>> {{docId, new List<int> {position}}};
                _terms.Add(termValue, docs);
            }
            else
            {
                IList<int> positions;
                if (!docs.TryGetValue(docId, out positions))
                {
                    positions = new List<int>();
                    docs.Add(docId, positions);
                }
                positions.Add(position);
            }
        }

        private void Flush()
        {
            var dir = Path.GetDirectoryName(_fileName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            using (var fs = File.Create(_fileName))
            {
                Serializer.Serialize(fs, _terms);
            }
        }

        public void Dispose()
        {
            Flush();
        }
    }
}