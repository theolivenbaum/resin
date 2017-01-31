using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Write;
using Resin.System;

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
        /// fileid/doc writer
        /// </summary>
        private readonly Dictionary<string, DocumentWriter> _docWriters;

        /// <summary>
        /// fileid/postings writer
        /// </summary>
        private readonly Dictionary<string, PostingsWriter> _postingsWriters;

        private readonly List<Document> _docs;

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _docWriters = new Dictionary<string, DocumentWriter>();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();
            _docs = new List<Document>();
            _postingsWriters = new Dictionary<string, PostingsWriter>();
        }

        public void Write(IEnumerable<Document> docs)
        {
            _docs.AddRange(docs);
        }
        
        private void WriteDocument(Document doc)
        {
            var fileId = doc.Id.ToDocFileId();
            DocumentWriter writer;

            if (!_docWriters.TryGetValue(fileId, out writer))
            {
                lock (DocumentWriter.SyncRoot)
                {
                    if (!_docWriters.TryGetValue(fileId, out writer))
                    {
                        var fileName = Path.Combine(_directory, fileId + ".doc");
                        var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                        var sr = new StreamWriter(fs, Encoding.ASCII);

                        writer = new DocumentWriter(sr);

                        _docWriters.Add(fileId, writer);
                    }
                }
                
            }
            writer.Write(doc);
        }

        private void WritePostings(Term term, IEnumerable<DocumentPosting> postings)
        {
            var fileId = term.ToPostingsFileId();
            PostingsWriter writer;

            if (!_postingsWriters.TryGetValue(fileId, out writer))
            {
                lock (PostingsWriter.SyncRoot)
                {
                    if (!_postingsWriters.TryGetValue(fileId, out writer))
                    {
                        var fileName = Path.Combine(_directory, fileId + ".pos");
                        var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                        var sr = new StreamWriter(fs, Encoding.ASCII);

                        writer = new PostingsWriter(sr);

                        _postingsWriters.Add(fileId, writer);
                    }
                }
            }
            writer.Write(term, postings);
        }

        private void WriteTriePath(string field, string value)
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

        private Stopwatch Time()
        {
            var timer = new Stopwatch();
            timer.Start();
            return timer;
        }

        public void Dispose()
        {
            var indexTime = Time();

            var termDocMatrix = new Dictionary<Term, List<DocumentPosting>>();

            var docTime = Time();

            foreach (var doc in _docs)
            {
                WriteDocument(doc);
            }

            Log.DebugFormat("wrote docs in {0}", docTime.Elapsed);

            var analyzeTime = Time();

            foreach(var doc in _docs)
            {
                var analyzed = _analyzer.AnalyzeDocument(doc);

                foreach (var term in analyzed.Terms)
                {
                    WriteTriePath(term.Key.Field, term.Key.Token);

                    List<DocumentPosting> weights;

                    if (termDocMatrix.TryGetValue(term.Key, out weights))
                    {
                        weights.Add(new DocumentPosting(analyzed.Id, term.Value));
                    }
                    else
                    {
                        termDocMatrix.Add(term.Key, new List<DocumentPosting> { new DocumentPosting(analyzed.Id, term.Value) });
                    }
                }
            }

            Log.DebugFormat("analyzed docs in {0}", analyzeTime.Elapsed);

            var docCountTime = Time();

            foreach (var doc in _docs)
            {
                foreach (var field in doc.Fields)
                {
                    _docCountByField.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
                }
            }

            Log.DebugFormat("counted docs per field in {0}", docCountTime.Elapsed);

            var trieTime = Time();

            Parallel.ForEach(_tries, kvp =>
            {
                var field = kvp.Key;
                var trie = kvp.Value;
                var fileName = Path.Combine(_directory, field.ToTrieFileId() + ".tri");

                trie.Serialize(fileName);
            });

            Log.DebugFormat("wrote tries in {0}", trieTime.Elapsed);

            var postingsTime = Time();

            foreach (var term in termDocMatrix)
            {
                WritePostings(term.Key, term.Value);
            }

            Log.DebugFormat("wrote postings in {0}", postingsTime.Elapsed);

            var ixInfo = new IxInfo
            {
                DocumentCount = new DocumentCount(new Dictionary<string, int>(_docCountByField))
            };
            ixInfo.Save(Path.Combine(_directory, "0.ix"));

            foreach (var pw in _postingsWriters.Values)
            {
                pw.Dispose();
            }

            foreach (var dw in _docWriters.Values)
            {
                dw.Dispose();
            }

            Log.DebugFormat("wrote index in {0}", indexTime.Elapsed);
        }
    }
}