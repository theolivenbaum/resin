using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Resin.IO;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        //private static readonly ILog Log = LogManager.GetLogger("TermFileAppender");
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

        private readonly TaskQueue<PostingsFile> _postingsWorker;

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _trieFiles = new Dictionary<string, Trie>();
            _postingsFiles = new Dictionary<string, PostingsFile>();
            _postingsContainers = new Dictionary<string, PostingsContainerFile>();
            _docContainers = new Dictionary<string, DocContainerFile>();
            _docWorker = new TaskQueue<Document>(1, PutDocInContainer);
            _postingsWorker = new TaskQueue<PostingsFile>(1, PutPostingsInContainer);

            var ixFileName = Path.Combine(directory, "0.ix");
            _ix = File.Exists(ixFileName) ? IxFile.Load(ixFileName) : new IxFile();
        }

        private void PutPostingsInContainer(PostingsFile posting)
        {
            var containerId = posting.Token.ToPostingHash();
            var containerFileName = Path.Combine(_directory, containerId + ".pl");
            var fieldTokenId = string.Format("{0}.{1}", posting.Field, posting.Token);
            PostingsContainerFile container;
            if (File.Exists(containerFileName))
            {
                if (!_postingsContainers.TryGetValue(containerId, out container))
                {
                    container = PostingsContainerFile.Load(containerFileName);
                    _postingsContainers[container.Id] = container;
                }
                container.Files[fieldTokenId] = posting;
            }
            else
            {
                if (!_postingsContainers.TryGetValue(containerId, out container))
                {
                    container = new PostingsContainerFile(containerId);
                    _postingsContainers[container.Id] = container;
                }
                //Log.InfoFormat("{0}:{1}", posting.Field, posting.Token);
                container.Files[fieldTokenId] = posting;
            }
        }

        private void PutDocInContainer(Document doc)
        {
            var containerId = doc.Id.ToDocHash();
            var containerFileName = Path.Combine(_directory, containerId + ".dl");
            DocContainerFile container;
            if (File.Exists(containerFileName))
            {
                if (!_docContainers.TryGetValue(containerId, out container))
                {
                    container = DocContainerFile.Load(containerFileName);
                    _docContainers[container.Id] = container;
                }
                Document existing;
                if (container.Files.TryGetValue(doc.Id, out existing))
                {
                    foreach (var field in doc.Fields)
                    {
                        existing.Fields[field.Key] = field.Value;
                    }
                }
                else
                {
                    container.Files[doc.Id] = doc;
                }
            }
            else
            {
                if (!_docContainers.TryGetValue(containerId, out container))
                {
                    container = new DocContainerFile(containerId);
                    _docContainers[container.Id] = container;
                }
                container.Files[doc.Id] = doc;
            }
        }

        public void Remove(string docId)
        {
            foreach (var field in _ix.Fields.Keys)
            {
                _ix.Fields[field].Remove(docId);
                var containerId = docId.ToDocHash();
                var containerFileName = Path.Combine(_directory, containerId + ".dl");
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
                        var pContainerId = token.ToPostingHash();
                        var pContainer = _postingsContainers[pContainerId];
                        pContainer.Files.Remove(fieldTokenId);
                        _postingsFiles.Remove(fieldTokenId);

                        var trie = GetTrie(field);
                        trie.Remove(token);
                    }
                    else
                    {
                        _postingsWorker.Enqueue(postingsFile);
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
            
            var postingsFile = GetPostingsFile(field, token);
            postingsFile.Postings[docId] = termFrequency;
            _postingsWorker.Enqueue(postingsFile);
        }

        private PostingsFile GetPostingsFile(string field, string token)
        {
            var fieldTokenId = string.Format("{0}.{1}", field, token);
            PostingsFile file;
            if (!_postingsFiles.TryGetValue(fieldTokenId, out file))
            {
                var id = token.ToPostingHash();
                var fileName = Path.Combine(_directory, id + ".pl");
                PostingsContainerFile container;
                if (!_postingsContainers.TryGetValue(id, out container))
                {
                    if (File.Exists(fileName))
                    {
                        container = PostingsContainerFile.Load(fileName);
                        _postingsContainers[id] = container;
                    }
                    else
                    {
                        container = new PostingsContainerFile(id);
                    }
                }
                _postingsContainers[id] = container;

                if (!container.Files.TryGetValue(fieldTokenId, out file))
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
                if (!Directory.GetFiles(_directory, field.ToTrieSearchPattern()).Any())
                {
                    file = new Trie();
                }
                else
                {
                    file = new LazyTrie(_directory, field);
                }
                _trieFiles[field] = file;
            }
            return file;
        }

        public void Dispose()
        {
            Parallel.ForEach(_trieFiles, trie =>
            {
                foreach (var child in trie.Value.DirectChildren())
                {
                    var fileNameWithoutExt = trie.Key.ToTrieFileNameWithoutExtension(child.Val);
                    string fileName = Path.Combine(_directory, fileNameWithoutExt + ".tr");
                    child.Save(fileName);  
                }
            });

            _docWorker.Dispose();
            _postingsWorker.Dispose();

            Parallel.ForEach(_postingsContainers.Values, container =>
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
            });
            //foreach (var container in _postingsContainers.Values)
            //{
            //    var fileName = Path.Combine(_directory, container.Id + ".pl");
            //    if (container.Files.Count > 0)
            //    {
            //        container.Save(fileName);
            //    }
            //    else
            //    {
            //        File.Delete(fileName);
            //    }
            //}
            Parallel.ForEach(_docContainers.Values, container =>
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
            });
            //foreach (var container in _docContainers.Values)
            //{
            //    var fileName = Path.Combine(_directory, container.Id + ".dl");
            //    if (container.Files.Count > 0)
            //    {
            //        container.Save(fileName);
            //    }
            //    else
            //    {
            //        File.Delete(fileName);
            //    }
            //}
            _ix.Save(Path.Combine(_directory, "0.ix"));
        }
    }
}