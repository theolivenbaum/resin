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
        private readonly IDictionary<string, FieldWriter> _fieldWriters;

        private readonly List<Term> _deletions;
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly DocumentWriter _docWriter;
        private bool _flushing;

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _docWriter = new DocumentWriter(_directory);
            _fieldWriters = new Dictionary<string, FieldWriter>();
            _fix = new FixFile();
            _deletions = new List<Term>();
        }

        // TODO: implement "delete by query"
        public void Remove(string field, string token)
        {
            _deletions.Add(new Term(field, token));
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

        private static string GetUniqueFileId(string dir)
        {
            var ticks = DateTime.Now.Ticks - Helper.BeginningOfTime.Ticks;
            var fileName = Path.Combine(dir, ticks + ".ix");
            return fileName;
        }

        private void Flush()
        {
            if (_flushing) return;
            _flushing = true;

            var ixFileName = GetUniqueFileId(_directory);
            var fixFileName = Path.Combine(_directory, Path.GetRandomFileName() + ".fix");
            var dixFileName = Path.Combine(_directory, Path.GetRandomFileName() + ".dix");

            _docWriter.Flush(dixFileName);

            foreach (var writer in _fieldWriters.Values)
            {
                writer.Flush();
            }

            _fix.Save(fixFileName);
            var ix = new IxFile(Path.GetFileName(fixFileName), Path.GetFileName(dixFileName), _deletions);
            ix.Save(ixFileName); // must be the last thing that happens in the flush, because as soon as the ix file exists this whole index will go live 
        }

        public void Dispose()
        {
            Flush();
        }
    }


}