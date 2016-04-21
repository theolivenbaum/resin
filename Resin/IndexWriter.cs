using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static readonly ILog Log = LogManager.GetLogger(typeof(IndexWriter));

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _fix = new FixFile();
            _fieldWriters = new Dictionary<string, FieldWriter>();
            _deletions = new List<Term>();
            _docs = new Dictionary<string, Document>();
        }

        // TODO: implement "delete by query"
        public void Remove(string field, string token)
        {
            _deletions.Add(new Term(field, token));
        }

        public void Write(Document doc)
        {
            _docs[doc.Id] = doc;
            foreach (var field in doc.Fields)
            {
                Analyze(doc.Id, field.Key, field.Value);
            }
        }

        public void Write(IDictionary<string, string> doc)
        {
            Write(new Document(doc));
        }

        private void Analyze(string docId, string field, string value)
        {
            string fileId;
            if (!_fix.FieldToFileId.TryGetValue(field, out fileId))
            {
                fileId = Path.GetRandomFileName();
                _fix.FieldToFileId.Add(field, fileId);
            }

            FieldWriter fw;
            if (!_fieldWriters.TryGetValue(fileId, out fw))
            {
                fw = new FieldWriter();
                _fieldWriters.Add(fileId, fw);
            }
            var termFrequencies = new Dictionary<string, int>();
            var analyze = field[0] != '_';
            if (analyze)
            {
                foreach (var token in _analyzer.Analyze(value))
                {
                    if (termFrequencies.ContainsKey(token)) termFrequencies[token] += 1;
                    else termFrequencies.Add(token, 1);
                }
            }
            else
            {
                if (termFrequencies.ContainsKey(value)) termFrequencies[value] += 1;
                else termFrequencies.Add(value, 1);
            }
            foreach (var token in termFrequencies)
            {
                fw.Write(docId, token.Key, token.Value, analyze);
            }
        }

        public IxFile Commit()
        {
            Log.Info("committing");

            var fieldFiles = new Dictionary<string, FieldFile>();
            var triFiles = new Dictionary<string, Trie>();
            foreach (var writer in _fieldWriters)
            {
                var fileId = writer.Key;
                fieldFiles.Add(fileId, writer.Value.FieldFile);
                triFiles.Add(fileId, writer.Value.Trie);
            }
            var deletions = new List<string>();
            var baseline = Helper.GetFileNameOfLatestIndex(_directory);
            if (baseline != null && _deletions.Count > 0)
            {
                var collector = new Collector(IxFile.Load(baseline), _directory);
                foreach (var term in _deletions)
                {
                    var docs = collector.Collect(new QueryParser(_analyzer).Parse(term.Field + ":" + term.Value), 0, int.MaxValue)
                        .Select(d=>d.DocId)
                        .ToList();
                    deletions.AddRange(docs);
                }
            }
            var commit = Helper.CreateIndex(_directory, ".ix", new DixFile(), _fix, _docs, fieldFiles, triFiles, deletions);
            return commit;
        }
    }
}