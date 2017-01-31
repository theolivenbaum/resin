using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Resin.IO;
using Resin.IO.Write;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private static readonly ILog Log = LogManager.GetLogger(typeof(IndexWriter));

        /// <summary>
        /// field/doc count
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _docCountByField;

        /// <summary>
        /// field/trie
        /// </summary>
        private readonly Dictionary<string, LcrsTrie> _tries;

        /// <summary>
        /// fileid/file
        /// </summary>
        private readonly ConcurrentDictionary<string, DocumentWriter> _docWriters;

        private readonly List<Document> _docs;

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _docWriters = new ConcurrentDictionary<string, DocumentWriter>();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();
            _docs = new List<Document>();
        }

        public void Write(IEnumerable<Document> docs)
        {
            _docs.AddRange(docs);
        }
        
        private void Write(Document doc)
        {
            var fileId = doc.Id.ToDocFileId();
            DocumentWriter writer;
            if (!_docWriters.TryGetValue(fileId, out writer))
            {
                var fileName = Path.Combine(_directory, fileId + ".doc");
                var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                var sr = new StreamWriter(fs, Encoding.ASCII);

                writer = new DocumentWriter(sr);

                _docWriters.AddOrUpdate(fileId, writer, (s, file) => file);
            }
            _docWriters[fileId].Write(doc);
        }

        private void WriteToTrie(string field, string value)
        {
            if (field == null) throw new ArgumentNullException("field");
            if (value == null) throw new ArgumentNullException("value");

            var trie = GetTrie(field);
            trie.Add(value);
        }

        private LcrsTrie GetTrie(string field)
        {
            LcrsTrie trie;
            if (!_tries.TryGetValue(field, out trie))
            {
                trie = new LcrsTrie('\0', false);
                _tries[field] = trie;
            }
            return trie;
        }

        public void Dispose()
        {
            var timer = new Stopwatch();
            timer.Start();

            var termDocMatrix = new Dictionary<Term, List<DocumentPosting>>();

            Log.Debug("analyzing documents");

            foreach (var doc in _docs)
            {
                Write(doc);
                var analyzed = _analyzer.AnalyzeDocument(doc);
                foreach (var term in analyzed.Terms)
                {
                    WriteToTrie(term.Key.Field, term.Key.Token);
                    List<DocumentPosting> weights;
                    if (termDocMatrix.TryGetValue(term.Key, out weights))
                    {
                        weights.Add(new DocumentPosting(analyzed.Id, (int)term.Value));
                    }
                    else
                    {
                        termDocMatrix.Add(term.Key, new List<DocumentPosting> { new DocumentPosting(analyzed.Id, (int)term.Value) });
                    }
                }
                foreach (var field in doc.Fields)
                {
                    _docCountByField.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
                }
            }

            Log.Debug("writing tries");

            Parallel.ForEach(_tries, kvp =>
            {
                var field = kvp.Key;
                var trie = kvp.Value;
                var fileName = Path.Combine(_directory, field.ToTrieFileId() + ".tri");
                trie.Serialize(fileName);
            });

            foreach (var dw in _docWriters.Values)
            {
                dw.Dispose();
            }

            Log.Debug("writing postings");

            var postings = new Dictionary<Term, int>();

            var fs = new FileStream(Path.Combine(_directory, "0.pos"), FileMode.Create, FileAccess.Write, FileShare.None);
            var sw = new StreamWriter(fs, Encoding.Unicode);
            using (var postingsWriter = new PostingsWriter(sw))
            {
                var row = 0;
                foreach (var term in termDocMatrix)
                {
                    postings.Add(term.Key, row++);
                    postingsWriter.Write(term.Value);
                } 
            }

            Log.Debug("writing index info");

            var ixInfo = new IndexInfo
            {
                PostingsAddressByTerm = postings,
                DocumentCount = new DocumentCount(new Dictionary<string, int>(_docCountByField))
            };
            ixInfo.Save(Path.Combine(_directory, "0.ix"));

            Log.DebugFormat("wrote index in {0}", timer.Elapsed);
        }
    }
}