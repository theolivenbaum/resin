using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Resin.IO;
using Resin.IO.Write;
using Resin.Sys;

namespace Resin
{
    public class Index
    {
        private readonly IxInfo _ixInfo;
        private readonly IEnumerable<Document> _documents;
        private readonly Dictionary<string, LcrsTrie> _tries;
        private readonly IDictionary<Term, List<DocumentPosting>> _postingsMatrix;
        private static readonly ILog Log = LogManager.GetLogger(typeof(Index));
        private readonly Dictionary<string, DocumentWriter> _docWriters;
        private readonly Dictionary<string, PostingsWriter> _postingsWriters;

        public IxInfo Info { get { return _ixInfo; } }

        public Index(
            IxInfo ixInfo,
            IEnumerable<Document> documents, 
            IDictionary<Term, List<DocumentPosting>> postingsMatrix, 
            Dictionary<string, LcrsTrie> tries)
        {
            _ixInfo = ixInfo;
            _documents = documents;
            _postingsMatrix = postingsMatrix;
            _tries = tries;
            _docWriters = new Dictionary<string, DocumentWriter>();
            _postingsWriters = new Dictionary<string, PostingsWriter>();
        }

        public string Serialize(string directory)
        {
            var indexTime = Time();

            var docs = EnqueueWriteDocuments(directory);
            var tries = EnqueueSerializeTries(directory);
            var postings = EnqueueSerializePostings(_postingsMatrix, directory);

            _ixInfo.Save(Path.Combine(directory, _ixInfo.Name + ".ix"));

            Task.WaitAll(docs, postings, tries);

            Cleanup();
            
            Log.DebugFormat("serializing took {0}", indexTime.Elapsed);

            return Path.Combine(directory, _ixInfo.Name + ".ix");
        }

        private Task EnqueueWriteDocuments(string directory)
        {
            return Task.Run(() =>
            {
                foreach (var doc in _documents)
                {
                    WriteDocument(doc, directory);
                }
            });
        }

        private Task EnqueueSerializePostings(IDictionary<Term, List<DocumentPosting>> postingsMatrix, string directory)
        {
            return Task.Run(() =>
            {
                using (var postingsWorker = new TaskQueue<Tuple<Term, IEnumerable<DocumentPosting>>>(1, t => WritePostings(t.Item1, t.Item2, directory)))
                {
                    foreach (var term in postingsMatrix)
                    {
                        postingsWorker.Enqueue(new Tuple<Term, IEnumerable<DocumentPosting>>(term.Key, term.Value));
                    }
                }
            });
        }

        private Task EnqueueSerializeTries(string directory)
        {
            return Task.Run(() =>
            {
                using (var work = new TaskQueue<Tuple<string, LcrsTrie>>(Math.Max(_tries.Count - 1, 1), w=>DoSerialize(w, directory)))
                {
                    foreach (var t in _tries)
                    {
                        work.Enqueue(new Tuple<string, LcrsTrie>(t.Key, t.Value));
                    }
                }
            });
        }

        private void DoSerialize(Tuple<string, LcrsTrie> trieEntry, string directory)
        {
            var field = trieEntry.Item1;
            var trie = trieEntry.Item2;
            var fileName = Path.Combine(directory, string.Format("{0}-{1}.tri", _ixInfo.Name, field.ToTrieFileId()));
            trie.SerializeToTextFile(fileName);
        }

        private void WriteDocument(Document doc, string directory)
        {
            var fileId = doc.Id.ToDocFileId();
            DocumentWriter writer;

            if (!_docWriters.TryGetValue(fileId, out writer))
            {
                lock (DocumentWriter.SyncRoot)
                {
                    if (!_docWriters.TryGetValue(fileId, out writer))
                    {
                        var fileName = Path.Combine(directory, string.Format("{0}-{1}.doc", _ixInfo.Name, fileId));
                        var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                        var sr = new StreamWriter(fs, Encoding.Unicode);

                        writer = new DocumentWriter(sr);

                        _docWriters.Add(fileId, writer);
                    }
                }
            }
            writer.Write(doc);
        }

        private void WritePostings(Term term, IEnumerable<DocumentPosting> postings, string directory)
        {
            var fileId = term.ToPostingsFileId();
            PostingsWriter writer;

            if (!_postingsWriters.TryGetValue(fileId, out writer))
            {
                lock (PostingsWriter.SyncRoot)
                {
                    if (!_postingsWriters.TryGetValue(fileId, out writer))
                    {
                        var fileName = Path.Combine(directory, string.Format("{0}-{1}.pos", _ixInfo.Name, fileId));
                        var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                        var sw = new StreamWriter(fs, Encoding.Unicode);

                        writer = new PostingsWriter(sw);

                        _postingsWriters.Add(fileId, writer);
                    }
                }
            }
            writer.Write(term, postings);
        }

        private Stopwatch Time()
        {
            var timer = new Stopwatch();
            timer.Start();
            return timer;
        }

        private void Cleanup()
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