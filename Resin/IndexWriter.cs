using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using log4net;
using Resin.IO;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private static readonly ILog Log = LogManager.GetLogger(typeof(IndexWriter));

        /// <summary>
        /// field/trie
        /// </summary>
        private readonly ConcurrentDictionary<string, Trie> _trieFiles;

        /// <summary>
        /// field.token/postings
        /// </summary>
        private readonly ConcurrentDictionary<string, PostingsFile> _postingsFiles;

        /// <summary>
        /// containerid/file
        /// </summary>
        private readonly ConcurrentDictionary<string, DocContainerFile> _docContainers;

        private readonly ConcurrentStack<DocContainerFile> _docContainerStack; 

        /// <summary>
        /// containerid/file
        /// </summary>
        private readonly Dictionary<string, PostingsContainerFile> _postingsContainers;
    
        private readonly IxFile _ix;
        private readonly FileSystemWatcher _dfileWatcher;

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _trieFiles = new ConcurrentDictionary<string, Trie>();
            _postingsFiles = new ConcurrentDictionary<string, PostingsFile>();
            _postingsContainers = new Dictionary<string, PostingsContainerFile>();
            _docContainers = new ConcurrentDictionary<string, DocContainerFile>();
            _docContainerStack = new ConcurrentStack<DocContainerFile>();

            var ixFileName = Path.Combine(directory, "0.ix");
            _ix = File.Exists(ixFileName) ? IxFile.Load(ixFileName) : new IxFile();
            
            _dfileWatcher = new FileSystemWatcher(_directory, "*.do") {NotifyFilter = NotifyFilters.LastWrite};
            _dfileWatcher.Changed += DfileChanged;
            _dfileWatcher.EnableRaisingEvents = true;
        }

        private void DfileChanged(object sender, FileSystemEventArgs e)
        {
            TryMoveDocFileIntoContainer(e.FullPath);
        }

        private void TryMoveDocFileIntoContainer(string fileName)
        {
            try
            {
                MoveDocFileIntoContainer(fileName);
            }
            catch (IOException ex)
            {
                Log.Debug(fileName, ex);
            }
        }

        private void MoveDocFileIntoContainer(string fileName)
        {
            var doc = Document.Load(fileName);
            if (doc == null) return;
            File.Delete(fileName);
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
                if (!_docContainerStack.TryPeek(out container) || container.Files.Count == 1000)
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
                        pContainer.Files.Remove(fieldTokenId);
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
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                var docFileName = Path.Combine(_directory, Path.GetRandomFileName() + ".do");
                doc.Save(docFileName);                
            }).Start();

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
            foreach (var trie in _trieFiles)
            {
                string fileName = Path.Combine(_directory, trie.Key.ToHash() + ".tr");
                trie.Value.Save(fileName);
            }
            foreach (var batch in _postingsFiles.Values.IntoBatches(10000))
            {
                var container = new PostingsContainerFile(Path.GetRandomFileName());
                foreach (var pf in batch)
                {
                    var id = string.Format("{0}.{1}", pf.Field, pf.Token);
                    container.Files.Add(id, pf);
                    _ix.PosContainers[id] = container.Id;
                }
                var fileName = Path.Combine(_directory, container.Id + ".pl");
                container.Save(fileName);
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

            _dfileWatcher.Dispose();

            foreach (var docFile in Directory.GetFiles(_directory, "*.do"))
            {
                MoveDocFileIntoContainer(docFile);
            }
            foreach (var container in _docContainers.Values.Concat(_docContainerStack))
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
}