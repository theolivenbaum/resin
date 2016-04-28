using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Resin.IO;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly int _docFileSize;
        private readonly int _postingFileSize;
        //private static readonly ILog Log = LogManager.GetLogger(typeof(IndexWriter));
        private readonly ConcurrentStack<DocContainerFile> _docContainerStack;
        private readonly ConcurrentStack<PostingsContainerFile> _posContainerStack;
        private readonly TaskQueue<Document> _docWorker;
        private readonly IxFile _ix;

        /// <summary>
        /// field/trie
        /// </summary>
        private readonly Dictionary<string, Trie> _trieFiles;

        /// <summary>
        /// field.token/postings
        /// </summary>
        private readonly Dictionary<string, PostingsFile> _postingsFiles;

        /// <summary>
        /// containerid/file
        /// </summary>
        private readonly Dictionary<string, DocContainerFile> _docContainers;
 
        /// <summary>
        /// containerid/file
        /// </summary>
        private readonly Dictionary<string, PostingsContainerFile> _postingsContainers;

        public IndexWriter(string directory, IAnalyzer analyzer, int docFileSize = 1000, int postingFileSize = 10000)
        {
            _directory = directory;
            _analyzer = analyzer;
            _docFileSize = docFileSize;
            _postingFileSize = postingFileSize;
            _trieFiles = new Dictionary<string, Trie>();
            _postingsFiles = new Dictionary<string, PostingsFile>();
            _postingsContainers = new Dictionary<string, PostingsContainerFile>();
            _posContainerStack = new ConcurrentStack<PostingsContainerFile>();
            _docContainers = new Dictionary<string, DocContainerFile>();
            _docContainerStack = new ConcurrentStack<DocContainerFile>();
            _docWorker = new TaskQueue<Document>(1, MoveDocIntoContainer);

            var ixFileName = Path.Combine(directory, "0.ix");
            _ix = File.Exists(ixFileName) ? IxFile.Load(ixFileName) : new IxFile();
        }

        private void MoveDocIntoContainer(Document doc)
        {
            DocContainerFile container;
            if (_ix.DocContainers.ContainsKey(doc.Id))
            {
                var containerId = _ix.DocContainers[doc.Id];
                if (!_docContainers.TryGetValue(containerId, out container))
                {
                    container = DocContainerFile.Load(Path.Combine(_directory, containerId + ".dl"));
                    _docContainers[container.Id] = container;
                }
                var existingDoc = container.Files[doc.Id];
                foreach (var field in doc.Fields)
                {
                    existingDoc.Fields[field.Key] = field.Value;
                }
            }
            else
            {
                if (!_docContainerStack.TryPeek(out container) || container.Files.Count == _docFileSize)
                {
                    container = new DocContainerFile(Path.GetRandomFileName());
                    _docContainerStack.Push(container);
                    _docContainers[container.Id] = container;
                }
                container.Files[doc.Id] = doc;
                _ix.DocContainers[doc.Id] = container.Id;
            }
        }

        public void Remove(string docId, string field)
        {
            if (_ix.Fields[field].ContainsKey(docId))
            {
                _ix.Fields[field].Remove(docId);
                var containerFileName = Path.Combine(_directory, _ix.DocContainers[docId] + ".dl");
                var container = DocContainerFile.Load(containerFileName);
                var doc = container.Files[docId];
                container.Files.Remove(docId);
                IEnumerable<string> tokens;
                if (field[0] == '_')
                {
                    tokens = new[] { doc.Fields[field] };
                }
                else
                {
                    tokens = _analyzer.Analyze(doc.Fields[field]);
                }
                foreach (var token in tokens)
                {
                    var fieldTokenId = string.Format("{0}.{1}", field, token);
                    var postingsFile = GetPostingsFile(field, token);
                    postingsFile.Postings.Remove(docId);
                    if (postingsFile.NumDocs() == 0)
                    {
                        var pContainer = _postingsContainers[_ix.PosContainers[fieldTokenId]];
                        
                        _ix.PosContainers.Remove(fieldTokenId);
                        pContainer.Files.Remove(fieldTokenId);
                        _postingsFiles.Remove(fieldTokenId);

                        var trie = GetTrie(field);
                        trie.Remove(token);
                    }
                }
            }  
        }

        public void Write(IDictionary<string, string> doc)
        {
            Write(new Document(doc));
        }

        public void Write(Document doc)
        {
            _docWorker.Enqueue(doc);  

            foreach (var field in doc.Fields)
            {
                Analyze(doc.Id, field.Key, field.Value);
            }
        }

        private void Analyze(string docId, string field, string value)
        {
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
                Write(docId, field, token.Key, token.Value);
            }
        }

        private void Write(string docId, string field, string token, int termFrequency)
        {
            if (!_ix.Fields.ContainsKey(field))
            {
                _ix.Fields.Add(field, new Dictionary<string, object>());
            }
            _ix.Fields[field][docId] = null;

            var trie = GetTrie(field);
            trie.Add(token);
            
            var pf = GetPostingsFile(field, token);
            pf.Postings[docId] = termFrequency;
            
        }

        private PostingsFile GetPostingsFile(string field, string token)
        {
            var fieldTokenId = string.Format("{0}.{1}", field, token);
            PostingsFile file;
            if (!_postingsFiles.TryGetValue(fieldTokenId, out file))
            {
                if (_ix.PosContainers.ContainsKey(fieldTokenId))
                {
                    var id = _ix.PosContainers[fieldTokenId];
                    var fileName = Path.Combine(_directory, id + ".pl");
                    var container = _postingsContainers.ContainsKey(id) ? _postingsContainers[id] : PostingsContainerFile.Load(fileName);
                    file = container.Pop(fieldTokenId);
                    _postingsContainers[id] = container;
                }
                else
                {
                    file = new PostingsFile(field, token);
                }
                _postingsFiles[fieldTokenId] = file;
            }
            return file;
        }

        private Trie GetTrie(string field)
        {
            Trie file;
            if (!_trieFiles.TryGetValue(field, out file))
            {
                var fileName = Path.Combine(_directory, field.ToHash() + ".tr");
                if (File.Exists(fileName))
                {
                    file = Trie.Load(fileName);
                }
                else
                {
                    file = new Trie();
                }
                _trieFiles[field] = file;
            }
            return file;
        }

        public void Dispose()
        {
            _docWorker.Dispose();
            foreach (var trie in _trieFiles)
            {
                string fileName = Path.Combine(_directory, trie.Key.ToHash() + ".tr");
                trie.Value.Save(fileName);
            }
            foreach (var pf in _postingsFiles.Values)
            {
                var fieldTokenId = string.Format("{0}.{1}", pf.Field, pf.Token);
                if (_ix.PosContainers.ContainsKey(fieldTokenId))
                {
                    var containerId = _ix.PosContainers[fieldTokenId];
                    var container = _postingsContainers[containerId];
                    container.Files[fieldTokenId] = pf;
                }
                else
                {
                    PostingsContainerFile container;
                    if (!_posContainerStack.TryPeek(out container) || container.Files.Count == _postingFileSize)
                    {
                        container = new PostingsContainerFile(Path.GetRandomFileName());
                        _posContainerStack.Push(container);
                    }
                    container.Files.Add(fieldTokenId, pf);
                    _postingsContainers[container.Id] = container;
                    _ix.PosContainers[fieldTokenId] = container.Id;
                }
            }
            foreach (var container in _postingsContainers.Values)
            {
                var fileName = Path.Combine(_directory, container.Id + ".pl");
                if (container.Files.Count > 0)
                {
                    container.Save(fileName);
                }
                else
                {
                    File.Delete(fileName);
                }
            }
            foreach (var container in _docContainers.Values)
            {
                var fileName = Path.Combine(_directory, container.Id + ".dl");
                if (container.Files.Count > 0)
                {
                    container.Save(fileName);
                }
                else
                {
                    File.Delete(fileName);
                }
            }
            _ix.Save(Path.Combine(_directory, "0.ix"));
        }
    }




    public class TaskQueue<T> : IDisposable where T : class
    {
        private readonly Action<T> _action;
        readonly object _sync = new object();
        readonly Thread[] _workers;
        readonly Queue<T> _tasks = new Queue<T>();

        public TaskQueue(int workerCount, Action<T> action)
        {
            _action = action;
            _workers = new Thread[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                (_workers[i] = new Thread(Consume)).Start();
                Trace.WriteLine("worker thread started");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                for (int i = 0; i < _workers.Length; i++) Enqueue(null);
                foreach (Thread worker in _workers)
                {
                    worker.Join();
                    Trace.WriteLine("worker thread joined");
                }
            }
        }

        public void Enqueue(T task)
        {
            lock (_sync)
            {
                _tasks.Enqueue(task);
                Monitor.PulseAll(_sync);
            }
        }

        void Consume()
        {
            while (true)
            {
                T task;
                lock (_sync)
                {
                    while (_tasks.Count == 0) Monitor.Wait(_sync);
                    task = _tasks.Dequeue();
                }
                if (task == null) return; //exit
                _action(task);
            }
        }

        public int Count { get { return _tasks.Count; } }
    }
}