using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin.IO;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        // field/fileid
        private readonly FixFile _fix;

        // field/writer
        private readonly IDictionary<string, FieldWriter> _fieldWriters;

        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly DocumentWriter _docWriter;
        private bool _flushing;

        private static readonly object Sync = new object();

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            _directory = directory;
            _analyzer = analyzer;
            _docWriter = new DocumentWriter(_directory);
            _fieldWriters = new Dictionary<string, FieldWriter>();
            _fix = new FixFile();
        }

        public void Write(IDictionary<string, string> doc)
        {
            var document = new Document(doc);
            foreach (var field in document.Fields)
            {
                string fieldFileId;
                if (!_fix.FieldIndex.TryGetValue(field.Key, out fieldFileId))
                {
                    fieldFileId = Path.GetRandomFileName();
                    _fix.FieldIndex.Add(field.Key, fieldFileId);
                }

                FieldWriter fw;
                if (!_fieldWriters.TryGetValue(fieldFileId, out fw))
                {
                    fw = new FieldWriter(Path.Combine(_directory, fieldFileId + ".f"));
                    _fieldWriters.Add(fieldFileId, fw);
                }
                var termFrequencies = new Dictionary<string, int>();
                var analyze = field.Key[0] != '_';
                if (analyze)
                {
                    foreach (var token in _analyzer.Analyze(field.Value))
                    {
                        if (termFrequencies.ContainsKey(token)) termFrequencies[token] += 1;
                        else termFrequencies.Add(token, 1);
                    }
                }
                else
                {
                    if (termFrequencies.ContainsKey(field.Value)) termFrequencies[field.Value] += 1;
                    else termFrequencies.Add(field.Value, 1);
                }
                foreach(var token in termFrequencies)
                {
                    fw.Write(document.Id, token.Key, token.Value);
                }
            }
            _docWriter.Write(document);
        }

        private static string ReserveIndexFileName(string dir)
        {
            lock (Sync)
            {
                var count = Directory.GetFiles(dir, "*.ix").Count(f => f.EndsWith(".tmp") == false);
                var fileName = Path.Combine(dir, count + ".ix");
                File.WriteAllText(fileName + ".tmp", string.Empty);
                return fileName;                
            }

        }

        private void Flush()
        {
            if (_flushing) return;
            _flushing = true;

            var ixFileName = ReserveIndexFileName(_directory);
            var fixFileName = Path.Combine(_directory, Path.GetRandomFileName() + ".fix");
            var dixFileName = Path.Combine(_directory, Path.GetRandomFileName() + ".dix");

            _docWriter.Flush(dixFileName);

            foreach (var writer in _fieldWriters.Values)
            {
                writer.Flush();
            }

            _fix.Save(fixFileName);
            new IxFile(Path.GetFileName(fixFileName), Path.GetFileName(dixFileName)).Save(ixFileName);
        }

        public void Dispose()
        {
            Flush();
        }
    }


}