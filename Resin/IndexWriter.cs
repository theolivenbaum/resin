using System;
using System.Collections.Concurrent;
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
        private readonly TaskQueue<Document> _docWorker;
        private readonly TaskQueue<PostingsFile> _postingsWorker;

        /// <summary>
        /// field/doc count
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _docCount;
        /// <summary>
        /// field/trie
        /// </summary>
        private readonly Dictionary<string, Trie> _tries;

        /// <summary>
        /// field.token/postings
        /// </summary>
        private readonly Dictionary<string, PostingsFile> _postingsFiles;

        /// <summary>
        /// containerid/file
        /// </summary>
        private readonly ConcurrentDictionary<string, DocumentFile> _docContainers;

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
            _docContainers = new ConcurrentDictionary<string, DocumentFile>();
            _docWorker = new TaskQueue<Document>(10, PutDocumentInContainer);
            _postingsWorker = new TaskQueue<PostingsFile>(10, PutPostingsInContainer);
            _tries = new Dictionary<string, Trie>();
            _docCount = new ConcurrentDictionary<string, int>();
        }

        public void Write(IEnumerable<Dictionary<string, string>> docs)
        {
            foreach (var doc in docs)
            {
                var d = new Document(doc);
                _docWorker.Enqueue(d);
                foreach (var field in d.Fields)
                {
                    Analyze(d.Id, field.Key, field.Value);
                }
            }

            Parallel.ForEach(_tries, kvp =>
            {
                var field = kvp.Key;
                var trie = kvp.Value;
                using (var writer = new TrieWriter(field.ToTrieContainerId(), _directory))
                {
                    writer.Write(trie);
                }
            });
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

        private void PutDocumentInContainer(Document doc)
        {
            var containerId = doc.Id.ToDocContainerId();
            DocumentFile container;
            if (!_docContainers.TryGetValue(containerId, out container))
            {
                container = new DocumentFile(_directory, containerId);
                _docContainers.AddOrUpdate(containerId, container, (s, file) => file);
            }
            container.Put(doc, _directory);
        }

        private void Analyze(string docId, string field, string value)
        {
            if (!_docCount.ContainsKey(field))
            {
                _docCount.AddOrUpdate(field, 1, (s, count) => count + 1);
            }

            var termCount = new Dictionary<string, object>();
            _scoringScheme.Eval(field, value, _analyzer, termCount);
            foreach (var term in termCount)
            {
                Write(docId, field, term.Key, term.Value);
            }
        }

        private void Write(string docId, string field, string term, object postingData)
        {
            if (docId == null) throw new ArgumentNullException("docId");
            if (field == null) throw new ArgumentNullException("field");
            if (term == null) throw new ArgumentNullException("term");
            if (postingData == null) throw new ArgumentNullException("postingData");

            var trie = GetTrie(field);
            trie.Add(term);

            var postingsFile = GetPostingsFile(field, term);
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

        private Trie GetTrie(string field)
        {
            Trie trie;
            if (!_tries.TryGetValue(field, out trie))
            {
                trie = new Trie();
                _tries[field] = trie;
            }
            return trie;
        }

        public void Dispose()
        {
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

            var ixInfo = new DocumentCountFile();
            foreach (var field in _docCount)
            {
                ixInfo.DocCount[field.Key] = field.Value;
            }
            ixInfo.Save(Path.Combine(_directory, "0.ix"));

            _postingsWorker.Dispose();
            _docWorker.Dispose();
            
        }
    }
}