using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Resin.IO;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly IScoringScheme _scoringScheme;
        //private static readonly ILog Log = LogManager.GetLogger("TermFileAppender");
        private readonly TaskQueue<Document> _docWorker;
        private readonly TaskQueue<PostingsFile> _postingsWorker;
        private readonly IList<string> _deletions;
        private readonly IxFile _ix;

        /// <summary>
        /// field/trie
        /// </summary>
        private readonly Dictionary<string, LazyTrie> _trieFiles;

        /// <summary>
        /// field.token/postings
        /// </summary>
        private readonly Dictionary<string, PostingsFile> _postingsFiles;

        /// <summary>
        /// containerid/file
        /// </summary>
        private readonly Dictionary<string, DocContainer> _docContainers;
 
        /// <summary>
        /// containerid/file
        /// </summary>
        private readonly Dictionary<string, PostingsContainer> _postingsContainers;

        public IndexWriter(string directory, IAnalyzer analyzer, IScoringScheme scoringScheme)
        {
            _directory = directory;
            _analyzer = analyzer;
            _scoringScheme = scoringScheme;
            _postingsFiles = new Dictionary<string, PostingsFile>();
            _postingsContainers = new Dictionary<string, PostingsContainer>();
            _docContainers = new Dictionary<string, DocContainer>();
            _docWorker = new TaskQueue<Document>(1, PutDocInContainer);
            _postingsWorker = new TaskQueue<PostingsFile>(1, PutPostingsInContainer);
            _deletions = new List<string>();
            _trieFiles = new Dictionary<string, LazyTrie>();

            var ixFileName = Path.Combine(directory, "1.ix");
            _ix = File.Exists(ixFileName) ? IxFile.Load(ixFileName) : new IxFile();
        }

        private void PutPostingsInContainer(PostingsFile posting)
        {
            var containerId = posting.Field.ToPostingsContainerId();
            PostingsContainer container;
            if (!_postingsContainers.TryGetValue(containerId, out container))
            {
                container = new PostingsContainer(_directory, containerId);
            }
            container.Put(posting);
            _postingsContainers[container.Id] = container;
        }

        private void PutDocInContainer(Document doc)
        {
            var containerId = doc.Id.ToDocContainerId();
            var containerFileName = Path.Combine(_directory, containerId + ".dc");
            DocContainer container;
            if (File.Exists(containerFileName))
            {
                if (!_docContainers.TryGetValue(containerId, out container))
                {
                    container = new DocContainer(_directory, containerId);
                }
                Document existing;
                if (container.TryGet(doc.Id, out existing))
                {
                    foreach (var field in doc.Fields)
                    {
                        existing.Fields[field.Key] = field.Value;
                    }
                    container.Remove(doc.Id);
                    container.Put(existing, _directory);
                }
                else
                {
                    container.Put(doc, _directory);
                }
            }
            else
            {
                if (!_docContainers.TryGetValue(containerId, out container))
                {
                    container = new DocContainer(_directory, containerId);
                    _docContainers[container.Id] = container;
                }
                container.Put(doc, _directory);
            }
            _docContainers[container.Id] = container;
        }

        public void Remove(string docId)
        {
            // TODO: should use a Collector to learn which docs to remove, which also works out well for DeleteByQuery.
            _deletions.Add(docId);
        }

        private void DoRemove(string docId)
        {
            foreach (var field in _ix.Fields.Keys)
            {
                if (!_ix.Fields[field].ContainsKey(docId))
                {
                    continue;
                }
                _ix.Fields[field].Remove(docId);
                var containerId = docId.ToDocContainerId();
                var containerFileName = Path.Combine(_directory, containerId + ".dc");
                DocContainer container;
                if (!_docContainers.TryGetValue(containerId, out container))
                {
                    container = new DocContainer(_directory, containerId);
                    _docContainers[containerId] = container;
                }
                if (File.Exists(containerFileName))
                {
                    Document doc;
                    if (container.TryGet(docId, out doc))
                    {
                        container.Remove(docId);
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
                                var pbucketId = field.ToPostingsContainerId();
                                var pContainer = _postingsContainers[pbucketId];
                                pContainer.Remove(token);
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
            }
        }

        public void Write(Dictionary<string, string> doc)
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
            var postingData = new Dictionary<string, object>();
            _scoringScheme.Eval(field, value, _analyzer, postingData);
            foreach (var token in postingData)
            {
                Write(docId, field, token.Key, token.Value);
            }
        }

        private void Write(string docId, string field, string token, object postingData)
        {
            if (docId == null) throw new ArgumentNullException("docId");
            if (field == null) throw new ArgumentNullException("field");
            if (token == null) throw new ArgumentNullException("token");
            if (postingData == null) throw new ArgumentNullException("postingData");

            if (!_ix.Fields.ContainsKey(field))
            {
                _ix.Fields.Add(field, new Dictionary<string, object>());
            }
            _ix.Fields[field][docId] = null;

            var trie = GetTrie(field);
            trie.Add(token);
            
            var postingsFile = GetPostingsFile(field, token);
            postingsFile.Postings[docId] = postingData;
            _postingsWorker.Enqueue(postingsFile);
        }

        private PostingsFile GetPostingsFile(string field, string token)
        {
            var fieldTokenId = string.Format("{0}.{1}", field, token);
            PostingsFile file;
            if (!_postingsFiles.TryGetValue(fieldTokenId, out file))
            {
                var containerId = field.ToPostingsContainerId();
                PostingsContainer container;
                if (!_postingsContainers.TryGetValue(containerId, out container))
                {
                    container = new PostingsContainer(_directory, containerId);
                }
                _postingsContainers[containerId] = container;

                if (!container.TryGet(token, out file))
                {
                    file = new PostingsFile(field, token);
                }
                _postingsFiles[fieldTokenId] = file;
            }
            return file;
        }

        private LazyTrie GetTrie(string field)
        {
            LazyTrie trie;
            if (!_trieFiles.TryGetValue(field, out trie))
            {
                trie = new LazyTrie(Path.Combine(_directory, field.ToTrieContainerId() + ".tc"));
                _trieFiles[field] = trie;
            }
            return trie;
        }

        public void Dispose()
        {
            foreach (var docId in _deletions)
            {
                DoRemove(docId);
            }

            Parallel.ForEach(_trieFiles, kvp =>
            {
                var field = kvp.Key;
                var trie = kvp.Value;
                using(var container = new TrieWriter(field.ToTrieContainerId()))
                {
                    trie.Save(container, _directory);
                }
            });

            _docWorker.Dispose();
            _postingsWorker.Dispose();

            Parallel.ForEach(_postingsContainers.Values, container =>
            {
                if (container.Count > 0)
                {
                    container.Flush(_directory);
                    container.Dispose();
                }
                else
                {
                    container.Dispose();
                    File.Delete(Path.Combine(_directory, container.Id + ".pc"));
                }
            });
            
            Parallel.ForEach(_docContainers.Values, container => container.Dispose());

            _ix.Save(Path.Combine(_directory, "1.ix"));
            var ixInfo = new IxInfo();
            foreach (var field in _ix.Fields)
            {
                ixInfo.DocCount[field.Key] = field.Value.Count;
            }
            ixInfo.Save(Path.Combine(_directory, "0.ix"));
        }
    }
}