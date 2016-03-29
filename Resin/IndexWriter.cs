using System;
using System.Collections.Generic;
using System.IO;
using Resin.IO;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        // field/fileid
        private readonly FixFile _fix;

        // field/writer
        private readonly IDictionary<string, FieldWriter> _fieldWriterCache;

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
            _fieldWriterCache = new Dictionary<string, FieldWriter>();
            _fix = new FixFile();
        }

        public void Write(Document doc)
        {
            foreach (var field in doc.Fields)
            {
                string fieldFileId;
                if (!_fix.FieldIndex.TryGetValue(field.Key, out fieldFileId))
                {
                    fieldFileId = Path.GetRandomFileName();
                    _fix.FieldIndex.Add(field.Key, fieldFileId);
                }

                FieldWriter fw;
                if (!_fieldWriterCache.TryGetValue(fieldFileId, out fw))
                {
                    fw = new FieldWriter(Path.Combine(_directory, fieldFileId + ".f"));
                    _fieldWriterCache.Add(fieldFileId, fw);
                }
                
                var termFrequencies = new Dictionary<string, int>();

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
            _docWriter.Write(doc);
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

            foreach (var writer in _fieldWriterCache.Values)
            {
                writer.Flush();
            }

            _fix.Save(fixFileName);
            new IxFile(fixFileName, dixFileName).Save(ixFileName);
            File.Copy(ixFileName, ixFileName.Substring(0, ixFileName.Length-4));
            File.Delete(ixFileName);
            _flushed = true;
        }

        public void Dispose()
        {
            Flush();
        }
    }


}