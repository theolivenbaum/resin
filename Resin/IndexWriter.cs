using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Resin.IO;

namespace Resin
{
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;

        /// <summary>
        /// field/doc count
        /// </summary>
        private readonly ConcurrentDictionary<string, int> _docCountByField;

        /// <summary>
        /// field/trie
        /// </summary>
        private readonly Dictionary<string, Trie> _tries;

        /// <summary>
        /// fileid/file
        /// </summary>
        private readonly ConcurrentDictionary<string, DocumentFile> _docFiles;

        private readonly List<IDictionary<string, string>> _docs;

        public IndexWriter(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            _docFiles = new ConcurrentDictionary<string, DocumentFile>();
            _tries = new Dictionary<string, Trie>();
            _docCountByField = new ConcurrentDictionary<string, int>();
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
            if (!_docFiles.TryGetValue(containerId, out container))
            {
                container = new DocumentFile(_directory, containerId);
                _docFiles.AddOrUpdate(containerId, container, (s, file) => file);
            }
            _docFiles[containerId].Put(doc, _directory);
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
                    _docCountByField.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
                }
            }
            Parallel.ForEach(_tries, kvp =>
            {
                var field = kvp.Key;
                var trie = kvp.Value;
                using (var writer = new TrieWriter(Path.Combine(_directory, field.ToTrieContainerId() + ".tc")))
                {
                    writer.Write(trie);
                }
            });

            Parallel.ForEach(_docFiles.Values, container => container.Dispose());

            var postings = new Dictionary<Term, int[]>(); 
            using(var fs = new FileStream(Path.Combine(_directory, "0.pos"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs, Encoding.Unicode))
            {
                var row = 0;
                foreach (var term in termDocMatrix)
                {
                    postings.Add(term.Key, new[] { row++, term.Value.Count});
                    foreach (var weight in term.Value)
                    {
                        writer.WriteLine(weight);
                    }
                } 
            }

            var ixInfo = new IndexInfo
            {
                Postings = postings,
                DocumentCount = new DocumentCount(new Dictionary<string, int>(_docCountByField))
            };
            ixInfo.Save(Path.Combine(_directory, "0.ix"));
        }
    }

    //public class Index
    //{
        
    //}

    //public class DocumentIndexer
    //{
    //    private IEnumerable<Document> _documents;

    //    public DocumentIndexer(IEnumerable<Document> documents)
    //    {
    //        _documents = documents;
    //    }

    //    public Index Create()
    //    {
            
    //    }
    //}
}