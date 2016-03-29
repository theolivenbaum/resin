using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        // field/fileid
        private readonly IDictionary<string, string> _fieldIndex;

        // field/writer
        private readonly IDictionary<string, FieldWriter> _fieldWriters;

        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly DocumentWriter _docWriter;
        private bool _flushed;

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            _directory = directory;
            _analyzer = analyzer;
            _docWriter = new DocumentWriter(_directory);
            _fieldWriters = new Dictionary<string, FieldWriter>();
            _fieldIndex = new Dictionary<string, string>();
        }

        public void Write(Document doc)
        {
            foreach (var field in doc.Fields)
            {
                string fieldFileId;
                if (!_fieldIndex.TryGetValue(field.Key, out fieldFileId))
                {
                    fieldFileId = Path.GetRandomFileName();
                    _fieldIndex.Add(field.Key, fieldFileId);
                }

                FieldWriter fw;
                if (!_fieldWriters.TryGetValue(fieldFileId, out fw))
                {
                    fw = new FieldWriter(Path.Combine(_directory, fieldFileId + ".f"));
                    _fieldWriters.Add(fieldFileId, fw);
                }
                
                var termFrequencies = new Dictionary<string, int>();
                _docWriter.Write(doc.Id, field.Key, field.Value);

                foreach (var token in _analyzer.Analyze(field.Value))
                {
                    if (termFrequencies.ContainsKey(token)) termFrequencies[token] += 1;
                    else termFrequencies.Add(token, 1);
                }

                foreach(var token in termFrequencies)
                {
                    fw.Write(doc.Id, token.Key, token.Value);
                }
            }
        }

        public static string ReserveIndexFileName(string dir)
        {
            var count = Directory.GetFiles(dir, "*.ix").Length;
            var fileName = Path.Combine(dir, count + ".ix.tmp");
            File.WriteAllText(fileName, string.Empty);
            return fileName;
        }

        private void Flush()
        {
            if (_flushed) return;

            var ixFileName = ReserveIndexFileName(_directory);
            var fixFileName = Path.Combine(_directory, Path.GetRandomFileName() + ".fix");
            var dixFileName = Path.Combine(_directory, Path.GetRandomFileName() + ".dix");

            _docWriter.Flush(dixFileName);

            foreach (var writer in _fieldWriters.Values)
            {
                writer.Dispose();
            }

            using (var fs = File.Create(fixFileName))
            {
                Serializer.Serialize(fs, _fieldIndex);
            }

            using (var fs = File.Create(ixFileName))
            {
                Serializer.Serialize(fs, new Index{DixFileName = dixFileName, FixFileName = fixFileName});
            }
            File.Copy(ixFileName, ixFileName.Substring(0, ixFileName.Length-4));
            File.Delete(ixFileName);
            _flushed = true;
        }

        public void Dispose()
        {
            Flush();
        }
    }

    [ProtoContract]
    public class Index
    {
        [ProtoMember(1)]
        public string FixFileName { get; set; }
        [ProtoMember(2)]
        public string DixFileName { get; set; }
    }
}