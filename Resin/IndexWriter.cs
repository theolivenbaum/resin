using System;
using System.Collections.Concurrent;
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
        //private readonly TaskQueue<Document> _docWorker;
        private static readonly object Sync = new object();

        /// <summary>
        /// field/doc count
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _docCount;

        /// <summary>
        /// field/trie
        /// </summary>
        private readonly Dictionary<string, Trie> _tries;

        /// <summary>
        /// containerid/file
        /// </summary>
        private readonly ConcurrentDictionary<string, DocumentFile> _docContainers;

        private readonly List<IDictionary<string, string>> _docs;

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _docContainers = new ConcurrentDictionary<string, DocumentFile>();
            //_docWorker = new TaskQueue<Document>(1, PutDocumentInContainer);
            _tries = new Dictionary<string, Trie>();
            _docCount = new ConcurrentDictionary<string, int>();
            _docs = new List<IDictionary<string, string>>();
        }

        public void Write(IEnumerable<Dictionary<string, string>> docs)
        {
            _docs.AddRange(docs);
        }
        
        private void PutDocumentInContainer(Document doc)
        {
            var containerId = doc.Id.ToDocContainerId();
            DocumentFile container;
            if (!_docContainers.TryGetValue(containerId, out container))
            {
                //lock (Sync)
                //{
                    container = new DocumentFile(_directory, containerId);
                    _docContainers.AddOrUpdate(containerId, container, (s, file) => file);
                //}
            }
            _docContainers[containerId].Put(doc, _directory);
        }

        private void WriteToTrie(string field, string value)
        {
            if (field == null) throw new ArgumentNullException("field");
            if (value == null) throw new ArgumentNullException("value");

            var trie = GetTrie(field);
            trie.Add(value);
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
            var termDocMatrix = new Dictionary<Term, List<DocumentWeight>>();
            foreach (var doc in _docs)
            {
                //_docWorker.Enqueue(new Document(doc));
                PutDocumentInContainer(new Document(doc));
                var analyzed = _analyzer.AnalyzeDocument(doc);
                foreach (var term in analyzed.Terms)
                {
                    WriteToTrie(term.Key.Field, term.Key.Token);
                    List<DocumentWeight> weights;
                    if (termDocMatrix.TryGetValue(term.Key, out weights))
                    {
                        weights.Add(new DocumentWeight(analyzed.Id, (int)term.Value));
                    }
                    else
                    {
                        termDocMatrix.Add(term.Key, new List<DocumentWeight> { new DocumentWeight(analyzed.Id, (int)term.Value) });
                    }
                }
                foreach (var field in doc)
                {
                    _docCount.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
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

            //_docWorker.Dispose();

            Parallel.ForEach(_docContainers.Values, container => container.Dispose());

            var termFileIds = new Dictionary<ulong, string>();
            var groups = termDocMatrix.GroupBy(x => x.Key.Token.Substring(0, 1).ToHash()).ToList();
            Parallel.ForEach(groups, group =>
            {
                var fileId = Path.GetRandomFileName();
                termFileIds.Add(group.Key, fileId);
                var fileName = Path.Combine(_directory, fileId + ".tdm");
                new TermDocumentMatrix {Weights = group.ToDictionary(x => x.Key, y => y.Value)}
                    .Save(fileName);
            });

            var ixInfo = new IndexInfo
            {
                TermFileIds = termFileIds,
                DocumentCount = new DocumentCount(new Dictionary<string, int>(_docCount))
            };
            ixInfo.Save(Path.Combine(_directory, "0.ix"));
        }
    }
}