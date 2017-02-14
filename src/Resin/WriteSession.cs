using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Write;
using Resin.Sys;

namespace Resin
{
    public class WriteSession : IDisposable
    {
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly IEnumerable<Document> _documents;
        private static readonly ILog Log = LogManager.GetLogger(typeof(WriteSession));

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

        private readonly string _indexName;

        public WriteSession(string directory, IAnalyzer analyzer, IEnumerable<Document> documents)
        {
            _directory = directory;
            _analyzer = analyzer;
            _documents = documents;
            _docWriters = new Dictionary<string, DocumentWriter>();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();
            _postingsWriters = new Dictionary<string, PostingsWriter>();
            _indexName = ToolBelt.GetChronologicalFileId();
        }

        public string Write()
        {
            var analyzeTime = Time();
            var analyzedDocs = Analyze(_documents);   
            var postings = BuildPostingsMatrix(analyzedDocs);

            Log.DebugFormat(" analyzed documents in {0}", analyzeTime.Elapsed);

            var serializeTime = Time();
            var postingsThread = EnqueueSerialize(postings);
            var trieThread = SerializeTries();
            var ixThread = SaveIxInfo();

            trieThread.Join();
            ixThread.Join();
            postingsThread.Join();

            Log.DebugFormat("serializing took {0}", serializeTime.Elapsed);

            return _indexName;
        }

        private IList<AnalyzedDocument> Analyze(IEnumerable<Document> documents)
        {
            var analyzedDocs = new List<AnalyzedDocument>();

            using (var trieBuilder = new TaskQueue<AnalyzedDocument>(1, BuildTree))
            using (var docWriter = new TaskQueue<Document>(1, WriteDocument))
            {
                foreach (var doc in documents)
                {
                    docWriter.Enqueue(doc);

                    var analyzedDoc = _analyzer.AnalyzeDocument(doc);
                    analyzedDocs.Add(analyzedDoc);

                    trieBuilder.Enqueue(analyzedDoc);

                    foreach (var field in doc.Fields)
                    {
                        _docCountByField.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
                    }
                }
            }

            return analyzedDocs;
        }

        private Thread EnqueueSerialize(Dictionary<Term, List<DocumentPosting>> postingsMatrix)
        {
            var thread = new Thread(() =>
            {
                using (var postingsWorker = new TaskQueue<Tuple<Term, IEnumerable<DocumentPosting>>>(1, t => WritePostings(t.Item1, t.Item2)))
                {
                    foreach (var term in postingsMatrix)
                    {
                        postingsWorker.Enqueue(new Tuple<Term, IEnumerable<DocumentPosting>>(term.Key, term.Value));
                    }
                }
            });
            thread.Start();
            return thread;
        }

        private Thread SaveIxInfo()
        {
            var thread = new Thread(() =>
            {
                var ixInfo = new IxInfo
                {
                    Name = _indexName,
                    DocumentCount = new DocumentCount(new Dictionary<string, int>(_docCountByField))
                };
                ixInfo.Save(Path.Combine(_directory, string.Format("{0}.ix", _indexName)));
            });
            thread.Start();
            return thread;
        }

        private Thread SerializeTries()
        {
            var thread = new Thread(() =>
            {
                using (var work  = new TaskQueue<Tuple<string, LcrsTrie>>(Math.Max(_tries.Count - 1, 1), DoSerialize))
                {
                    foreach (var t in _tries)
                    {
                        work.Enqueue(new Tuple<string, LcrsTrie>(t.Key, t.Value));
                    }
                }
            });
            thread.Start();
            return thread;
        }

        private void DoSerialize(Tuple<string, LcrsTrie> trieEntry)
        {
            var field = trieEntry.Item1;
            var trie = trieEntry.Item2;
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}.tri", _indexName, field.ToTrieFileId()));
            trie.Serialize(fileName);
        }

        private void BuildTree(AnalyzedDocument analyzedDoc)
        {
            foreach (var term in analyzedDoc.Terms)
            {
                WriteToTrie(term.Key.Field, term.Key.Word.Value);
            }
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
                        var fileName = Path.Combine(_directory, string.Format("{0}-{1}.doc", _indexName, fileId));
                        var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                        var sr = new StreamWriter(fs, Encoding.Unicode);

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
                        var fileName = Path.Combine(_directory, string.Format("{0}-{1}.pos", _indexName, fileId));
                        var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                        var sw = new StreamWriter(fs, Encoding.Unicode);

                        writer = new PostingsWriter(sw);

                        _postingsWriters.Add(fileId, writer);
                    }
                }
            }
            writer.Write(term, postings);
        }

        private Dictionary<Term, List<DocumentPosting>> BuildPostingsMatrix(IList<AnalyzedDocument> analyzedDocs)
        {
            var postingsMatrix = new Dictionary<Term, List<DocumentPosting>>();

            foreach (var doc in analyzedDocs)
            {
                foreach (var term in doc.Terms)
                {
                    List<DocumentPosting> weights;

                    if (postingsMatrix.TryGetValue(term.Key, out weights))
                    {
                        weights.Add(new DocumentPosting(doc.Id, term.Value));
                    }
                    else
                    {
                        postingsMatrix.Add(term.Key, new List<DocumentPosting> { new DocumentPosting(doc.Id, term.Value) });
                    }
                }
            }
            return postingsMatrix;
        }

        private Stopwatch Time()
        {
            var timer = new Stopwatch();
            timer.Start();
            return timer;
        }

        public void Dispose()
        {
            foreach (var pw in _postingsWriters.Values)
            {
                pw.Dispose();
            }

            foreach (var dw in _docWriters.Values)
            {
                dw.Dispose();
            }
        }
    }
}