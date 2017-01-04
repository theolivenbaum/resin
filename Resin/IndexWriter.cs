using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Resin.IO;

namespace Resin
{
    public class AnalyzedDocument
    {
        private readonly string _id;
        private readonly IDictionary<Term, object> _terms;

        public IDictionary<Term, object> Terms { get { return _terms; } }
        public string Id { get { return _id; } }

        public AnalyzedDocument(string id, IDictionary<string, IDictionary<string, object>> analyzedTerms)
        {
            _id = id;
            _terms = new Dictionary<Term, object>();
            foreach (var field in analyzedTerms)
            {
                foreach (var term in field.Value)
                {
                    var key = new Term(field.Key, term.Key);
                    object data;
                    if (!_terms.TryGetValue(key, out data))
                    {
                        _terms.Add(key, term.Value);
                    }
                    else
                    {
                        _terms[key] = (int) data + (int) term.Value;
                    }
                }
            }
        }
    }

    [Serializable]
    public class DocumentWeight : IEquatable<DocumentWeight>
    {
        public string DocumentId { get; private set; }
        public int Weight { get; private set; }

        public DocumentWeight(string documentId, int weight)
        {
            DocumentId = documentId;
            Weight = weight;
        }

        public bool Equals(DocumentWeight other)
        {
            if (other == null) return false;
            return other.DocumentId == DocumentId && other.Weight == Weight;
        }

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash*7) + DocumentId.GetHashCode();
            hash = (hash*7) + Weight.GetHashCode();
            return hash;
        }
    }

    [Serializable]
    public class Term : IEquatable<Term>
    {
        public string Field { get; private set; }
        public string Token { get; private set; }

        public Term(string field, string token)
        {
            Field = field;
            Token = token;
        }
        public bool Equals(Term other)
        {
            if (other == null) return false;
            return other.Field == Field && other.Token == Token;
        }
        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Field.GetHashCode();
            hash = (hash * 7) + Token.GetHashCode();
            return hash;
        }
    } 
    public class IndexWriter : IDisposable
    {
        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly TaskQueue<Document> _docWorker;
        private static readonly object _sync = new object();
        private readonly TermDocumentMatrix _termDocMatrix;

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
            _docWorker = new TaskQueue<Document>(1, PutDocumentInContainer);
            _tries = new Dictionary<string, Trie>();
            _docCount = new ConcurrentDictionary<string, int>();
            _termDocMatrix = new TermDocumentMatrix();
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
                lock (_sync)
                {
                    container = new DocumentFile(_directory, containerId);
                    _docContainers.AddOrUpdate(containerId, container, (s, file) => file);
                }
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
            var ix = new Dictionary<Term, List<DocumentWeight>>();
            foreach (var doc in _docs)
            {
                _docWorker.Enqueue(new Document(doc));
                var analyzed = _analyzer.AnalyzeDocument(doc);
                foreach (var term in analyzed.Terms)
                {
                    WriteToTrie(term.Key.Field, term.Key.Token);
                    List<DocumentWeight> weights;
                    if (ix.TryGetValue(term.Key, out weights))
                    {
                        weights.Add(new DocumentWeight(analyzed.Id, (int)term.Value));
                    }
                    else
                    {
                        ix.Add(term.Key, new List<DocumentWeight> { new DocumentWeight(analyzed.Id, (int)term.Value) });
                    }
                }
                foreach (var field in doc)
                {
                    _docCount.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
                }
            }
            _termDocMatrix.Weights = ix;
            Parallel.ForEach(_tries, kvp =>
            {
                var field = kvp.Key;
                var trie = kvp.Value;
                using (var writer = new TrieWriter(field.ToTrieContainerId(), _directory))
                {
                    writer.Write(trie);
                }
            });

            _docWorker.Dispose();

            Parallel.ForEach(_docContainers.Values, container => container.Dispose());

            var ixInfo = new IndexInfo {DocumentCount = new DocumentCount(_docCount.ToDictionary(x=>x.Key, y=>y.Value))};
            foreach (var field in _docCount)
            {
                ixInfo.DocumentCount.DocCount[field.Key] = field.Value;
            }
            _termDocMatrix.Save(Path.Combine(_directory, "0.tdm"));
            ixInfo.Save(Path.Combine(_directory, "0.ix"));
        }
    }
}