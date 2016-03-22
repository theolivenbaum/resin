using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly IDictionary<string, int> _fieldIndex; 
        private readonly IDictionary<int, FieldWriter> _fieldWriters;
        private readonly string _fieldIndexFileName;
        private readonly DocumentWriter _docWriter;

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;

            _docWriter = new DocumentWriter(directory);
            _fieldWriters = new Dictionary<int, FieldWriter>();
            _fieldIndexFileName = Path.Combine(_directory, "fld.ix");

            if (File.Exists(_fieldIndexFileName))
            {
                using (var fs = File.OpenRead(_fieldIndexFileName))
                {
                    _fieldIndex = Serializer.Deserialize<Dictionary<string, int>>(fs);
                }
            }
            else
            {
                _fieldIndex = new Dictionary<string, int>();
            }

            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        }

        public void Write(Document doc)
        {
            foreach (var field in doc.Fields)
            {
                int fieldId;
                if (!_fieldIndex.TryGetValue(field.Key, out fieldId))
                {
                    fieldId = GetNextFreeFieldId();
                    _fieldIndex.Add(field.Key, fieldId);
                }

                FieldWriter fw;
                if (!_fieldWriters.TryGetValue(fieldId, out fw))
                {
                    var fileName = Path.Combine(_directory, fieldId + ".fld");
                    fw = new FieldWriter(fileName);
                    _fieldWriters.Add(fieldId, fw);
                }
                
                var docTokensAndTheirPositions = new Dictionary<string, List<int>>();
                foreach (var value in field.Value)
                {
                    _docWriter.Write(doc.Id, field.Key, value);

                    var tokens = _analyzer.Analyze(value);
                    for (int position = 0; position < tokens.Length; position++)
                    {
                        var token = tokens[position];
                        List<int> positions;
                        if (!docTokensAndTheirPositions.TryGetValue(token, out positions))
                        {
                            positions = new List<int>();
                            docTokensAndTheirPositions.Add(token, positions);
                        }
                        positions.Add(position);
                    }
                }

                foreach(var tokenAndItsPositions in docTokensAndTheirPositions)
                {
                    foreach (var position in tokenAndItsPositions.Value)
                    {
                        fw.Write(doc.Id, tokenAndItsPositions.Key, position);
                    }
                }
            }
        }

        private int GetNextFreeFieldId()
        {
            return _fieldIndex.Count;
        }

        private void Flush()
        {
            _docWriter.Dispose();

            foreach (var fieldFile in _fieldWriters)
            {
                fieldFile.Value.Dispose();
            }

            using (var fs = File.Create(_fieldIndexFileName))
            {
                Serializer.Serialize(fs, _fieldIndex);
            }
        }

        public void Dispose()
        {
            Flush();
        }
    }
}