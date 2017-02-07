using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Write;
using Resin.Sys;

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

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _docWriters = new Dictionary<string, DocumentWriter>();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();
            _postingsWriters = new Dictionary<string, PostingsWriter>();
        }

        public void Write(IEnumerable<Document> documents)
        {
            var indexTime = Time();
            var analyzedDocs = new List<AnalyzedDocument>();
            var analyzeTime = Time();

            using (var trieWorker = new TaskQueue<AnalyzedDocument>(1, BuildTree))
            using (var docWorker = new TaskQueue<Document>(1, WriteDocument))
            {
                foreach (var doc in documents)
                {
                    var analyzedDoc = _analyzer.AnalyzeDocument(doc);
                    analyzedDocs.Add(analyzedDoc);

                    trieWorker.Enqueue(analyzedDoc);
                    docWorker.Enqueue(doc);

                    foreach (var field in doc.Fields)
                    {
                        _docCountByField.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
                    }
                }
            }
            Log.DebugFormat("stored and analyzed documents in {0}", analyzeTime.Elapsed);

            var trieThread = new Thread(() =>
            {
                var trieTime = Time();

                SerializeTries();

                Log.DebugFormat("serialized tries in {0}", trieTime.Elapsed);
            });
            trieThread.Start();

            var matrixBuildTime = Time();

            var postingsMatrix = BuildPostingsMatrix(analyzedDocs);

            Log.DebugFormat("built postings matrix in {0}", matrixBuildTime.Elapsed);

            var postingsThread = new Thread(() =>
            {
                var postingsTime = Time();

                foreach (var term in postingsMatrix)
                {
                    WritePostings(term.Key, term.Value);
                }

                Log.DebugFormat("serialized postings in {0}", postingsTime.Elapsed);
            });
            postingsThread.Start();
            
            var ixInfo = new IxInfo
            {
                DocumentCount = new DocumentCount(new Dictionary<string, int>(_docCountByField))
            };
            ixInfo.Save(Path.Combine(_directory, "0.ix"));

            trieThread.Join();
            postingsThread.Join();

            Log.DebugFormat("indexing took {0}", indexTime.Elapsed);
        }

        private void SerializeTries()
        {
            //foreach(var kvp in _tries)
            Parallel.ForEach(_tries, kvp =>
            {
                var field = kvp.Key;
                var trie = kvp.Value;
                var fileName = Path.Combine(_directory, field.ToTrieFileId() + ".tri");

                trie.Serialize(fileName);
            });
        }

        private void BuildTree(AnalyzedDocument analyzedDoc)
        {
            foreach (var term in analyzedDoc.Terms)
            {
                WriteToTrie(term.Key.Field, term.Key.Word.Value);
            }
        }

        private Dictionary<Term, List<DocumentPosting>> BuildPostingsMatrix(IEnumerable<AnalyzedDocument> analyzedDocs)
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
                        var fileName = Path.Combine(_directory, fileId + ".pos");
                        var fs = File.Open(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                        var sw = new StreamWriter(fs, Encoding.Unicode);

                        writer = new PostingsWriter(sw);

                        _postingsWriters.Add(fileId, writer);
                    }
                }
            }
            writer.Write(term, postings);
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