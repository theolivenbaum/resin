using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtoBuf;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly Analyzer _analyzer;
        private readonly bool _overwrite;
        private readonly IDictionary<string, int> _fieldIndex; 
        private readonly IDictionary<int, FieldWriter> _fieldWriters;

        public IndexWriter(string directory, Analyzer analyzer, bool overwrite = true)
        {
            _directory = directory;
            _analyzer = analyzer;
            _overwrite = overwrite;
            _fieldIndex = new Dictionary<string, int>();
            _fieldWriters = new Dictionary<int, FieldWriter>();
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        }

        public void Write(int docId, string field, string value)
        {
            int fieldId;
            if (!_fieldIndex.TryGetValue(field, out fieldId))
            {
                fieldId = GetNextFreeFieldId();
                _fieldIndex.Add(field, fieldId);
            }
            FieldWriter fw;
            if (!_fieldWriters.TryGetValue(fieldId, out fw))
            {
                var fileName = Path.Combine(_directory, fieldId + ".fld");
                fw = new FieldWriter(fileName);
                _fieldWriters.Add(fieldId, fw);
            }
            var terms = _analyzer.Analyze(value);
            for(int position = 0; position < terms.Length; position++)
            {
                fw.Write(docId, terms[position], position);
            }
            using (var dw = new DocumentWriter(Path.Combine(_directory, docId + ".d")))
            {
                dw.Write(field, value);
            }
        }

        private int GetNextFreeFieldId()
        {
            var liveFieldFiles = Directory.GetFiles(_directory, "*.fld");
            if (liveFieldFiles.Length > 0)
            {
                var highestLiveFieldId = liveFieldFiles
                .Select(f=>int.Parse(Path.GetFileNameWithoutExtension(f)))
                .OrderBy(i=>i)
                .Last();
                return highestLiveFieldId + _fieldIndex.Count + 1;
            }
            return _fieldIndex.Count;
        }

        public void Dispose()
        {
            foreach (var fw in _fieldWriters.Values)
            {
                fw.Dispose();
            }
            var indexFileName = Path.Combine(_directory, "field.idx");
            IDictionary<string, IList<int>> fieldIndex;
            if (_overwrite)
            {
                fieldIndex = new Dictionary<string, IList<int>>();
                foreach (var entry in _fieldIndex)
                {
                    fieldIndex.Add(entry.Key, new List<int> { entry.Value });
                }
            }
            else
            {
                if (File.Exists(indexFileName))
                {
                    using (var fs = File.OpenRead(indexFileName))
                    {
                        fieldIndex = Serializer.Deserialize<Dictionary<string, IList<int>>>(fs);
                    }
                    foreach (var entry in _fieldIndex)
                    {
                        IList<int> fieldIds;
                        if (fieldIndex.TryGetValue(entry.Key, out fieldIds))
                        {
                            fieldIds.Add(entry.Value);
                        }
                        else
                        {
                            fieldIds = new List<int>{entry.Value};
                            fieldIndex.Add(entry.Key, fieldIds);
                        }
                    }
                }
                else
                {
                    fieldIndex = new Dictionary<string, IList<int>>();
                    foreach (var entry in _fieldIndex)
                    {
                        fieldIndex.Add(entry.Key, new List<int> { entry.Value });
                    }
                } 
            }

            var tmpFile = Path.Combine(_directory, "field.idx.tmp");
            using (var fs = File.Create(tmpFile))
            {
                Serializer.Serialize(fs, fieldIndex);
            }
            if(File.Exists(indexFileName)) File.Delete(indexFileName);
            File.Move(tmpFile, indexFileName);
            var indexedFields = fieldIndex.Values.SelectMany(l => l).ToList();
            foreach (var file in Directory.GetFiles(_directory, "*.fld"))
            {
                var fieldId = int.Parse(Path.GetFileNameWithoutExtension(file));
                if (!indexedFields.Contains(fieldId)) File.Delete(file);
            }
        }
    }
}
