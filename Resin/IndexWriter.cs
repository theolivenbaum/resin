using System.Collections.Generic;
using System.IO;
using log4net;
using Resin.IO;

namespace Resin
{
    public class IndexWriter
    {
        // field/fileid
        private readonly FixFile _fix;

        // field/writer
        private readonly Dictionary<string, FieldWriter> _fieldWriters;

        // docid/fields/value
        private readonly Dictionary<string, Document> _docs;

        private readonly List<Term> _deletions;
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private bool _flushing;
        private static readonly ILog Log = LogManager.GetLogger(typeof(IndexWriter));

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _fieldWriters = new Dictionary<string, FieldWriter>();
            _deletions = new List<Term>();
            _fix = new FixFile();
            _docs = new Dictionary<string, Document>();
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
                string fileId;
                if (!_fix.FieldToFileId.TryGetValue(field.Key, out fileId))
                {
                    fileId = Path.GetRandomFileName();
                    _fix.FieldToFileId.Add(field.Key, fileId);
                }

                FieldWriter fw;
                if (!_fieldWriters.TryGetValue(fileId, out fw))
                {
                    fw = new FieldWriter();
                    _fieldWriters.Add(fileId, fw);
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
                    fw.Write(document.Id, token.Key, token.Value, analyze);
                }
            }
            _docs[document.Id] = document; // this overwrites previous doc if same docId appears twice in the batch
        }

        public void Flush()
        {
            if (_flushing) return;
            _flushing = true;

            var commitDix = new DixFile();
            var fieldFiles = new Dictionary<string, FieldFile>();
            var triFiles = new Dictionary<string, Trie>();
            foreach (var writer in _fieldWriters)
            {
                var fileId = writer.Key;
                fieldFiles.Add(fileId, writer.Value.FieldFile);
                triFiles.Add(fileId, writer.Value.Trie);
            }
            var commit = Helper.Save(_directory, ".co", commitDix, _fix, _docs, fieldFiles, triFiles);
            var latestBaseline = Helper.GetFileNameOfLatestIndex(_directory);
            if (latestBaseline == null)
            {
                var ixFileName = Helper.GenerateNewChronologicalFileName(_directory, ".ix");
                commit.Save(ixFileName);
                Log.InfoFormat("new baseline {0}", ixFileName);
            }
            else
            {
                Log.Info("loading last baseline");
                var ix = IxFile.Load(latestBaseline);
                var dix = DixFile.Load(Path.Combine(_directory, ix.DixFileName));
                var fix = FixFile.Load(Path.Combine(_directory, ix.FixFileName));
                var optimizer = new Optimizer(_directory, latestBaseline, dix, fix);
                optimizer.Rebase();
                optimizer.Save();
            }
        }
    }
}