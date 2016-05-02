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
        private readonly IScoringScheme _scoringScheme;
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
        /// bucketId/file
        /// </summary>
        private readonly Dictionary<string, DocContainer> _docContainers;
 
        /// <summary>
        /// bucketId/file
        /// </summary>
        private readonly Dictionary<string, PostingsContainer> _postingsContainers;

        private readonly TaskQueue<PostingsFile> _postingsWorker;

        private readonly IList<string> _deletions;

        public IndexWriter(string directory, IAnalyzer analyzer, IScoringScheme scoringScheme)
        {
            _directory = directory;
            _analyzer = analyzer;
            _scoringScheme = scoringScheme;
            _trieFiles = new Dictionary<string, Trie>();
            _postingsFiles = new Dictionary<string, PostingsFile>();
            _postingsContainers = new Dictionary<string, PostingsContainer>();
            _docContainers = new Dictionary<string, DocContainer>();
            _docWorker = new TaskQueue<Document>(1, PutDocInContainer);
            _postingsWorker = new TaskQueue<PostingsFile>(1, PutPostingsInContainer);
            _deletions = new List<string>();

            var ixFileName = Path.Combine(directory, "1.ix");
            _ix = File.Exists(ixFileName) ? IxFile.Load(ixFileName) : new IxFile();
        }

        private void PutPostingsInContainer(PostingsFile posting)
        {
            var bucketId = posting.Token.ToPostingsBucket();
            var containerFileName = Path.Combine(_directory, bucketId + ".pl");
            var fieldTokenId = string.Format("{0}.{1}", posting.Field, posting.Token);
            PostingsContainer container;
            if (File.Exists(containerFileName))
            {
                if (!_postingsContainers.TryGetValue(bucketId, out container))
                {
                    container = PostingsContainer.Load(containerFileName);
                }
                container.Files[fieldTokenId] = posting;
            }
            else
            {
                if (!_postingsContainers.TryGetValue(bucketId, out container))
                {
                    container = new PostingsContainer(bucketId);
                }
                container.Files[fieldTokenId] = posting;
            }
            _postingsContainers[container.Id] = container;
        }

        private void PutDocInContainer(Document doc)
        {
            var bucketId = doc.Id.ToDocBucket();
            var containerFileName = Path.Combine(_directory, bucketId + ".dl");
            DocContainer container;
            if (File.Exists(containerFileName))
            {
                if (!_docContainers.TryGetValue(bucketId, out container))
                {
                    container = DocContainer.Load(containerFileName);
                }
                Document existing;
                if (container.TryGet(doc.Id, _directory, out existing))
                {
                    foreach (var field in doc.Fields)
                    {
                        existing.Fields[field.Key] = field.Value;
                    }
                    container.Put(existing, _directory);
                }
                else
                {
                    container.Put(doc, _directory);
                }
            }
            else
            {
                if (!_docContainers.TryGetValue(bucketId, out container))
                {
                    container = new DocContainer(bucketId);
                    _docContainers[container.Id] = container;
                }
                container.Put(doc, _directory);
            }
            _docContainers[container.Id] = container;
        }

        public void Remove(string docId)
        {
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
                var bucketId = docId.ToDocBucket();
                var containerFileName = Path.Combine(_directory, bucketId + ".dl");
                var container = DocContainer.Load(containerFileName);
                var doc = container.Get(docId, _directory);
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
                        var pbucketId = token.ToPostingsBucket();
                        var pContainer = _postingsContainers[pbucketId];
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
                var bucketId = token.ToPostingsBucket();
                var fileName = Path.Combine(_directory, bucketId + ".pl");
                PostingsContainer container;
                if (!_postingsContainers.TryGetValue(bucketId, out container))
                {
                    if (File.Exists(fileName))
                    {
                        container = PostingsContainer.Load(fileName);
                        _postingsContainers[bucketId] = container;
                    }
                    else
                    {
                        container = new PostingsContainer(bucketId);
                    }
                }
                _postingsContainers[bucketId] = container;

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
            //TODO:this is not the time to be doing this
            foreach (var docId in _deletions)
            {
                DoRemove(docId);
            }

            Parallel.ForEach(_trieFiles, kvp =>
            {
                var field = kvp.Key;
                var trie = kvp.Value;
                foreach (var child in trie.Dirty())
                {
                    var fileNameWithoutExt = field.ToTrieBucket(child.Val);
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
            Parallel.ForEach(_docContainers.Values, container =>
            {
                var fileName = Path.Combine(_directory, container.Id + ".dl");
                if (container.Count > 0)
                {
                    container.Save(fileName);
                    container.Dispose();
                }
                else
                {
                    container.Dispose();
                    File.Delete(fileName);
                }
            });
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