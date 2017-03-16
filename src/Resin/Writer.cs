using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSharpTest.Net.Collections;
using CSharpTest.Net.Serialization;
using CSharpTest.Net.Synchronization;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Write;
using Resin.Sys;

namespace Resin
{
    public abstract class Writer : IDisposable
    {
        protected abstract IEnumerable<Document> ReadSource();

        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly string _indexName;
        private readonly Dictionary<string, LcrsTrie> _tries;
        private readonly object _sync = new object();
        private readonly ConcurrentDictionary<string, int> _docCountByField;
        private readonly DbDocumentWriter _docWriter;

        protected Writer(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            
            _indexName = Util.GetChronologicalFileId();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();
            _docWriter = new DbDocumentWriter(CreateDocumentDb());
        }

        public string Execute()
        {
            var trieBuilders = new List<Task>();
            var index = 0;
            var matrix = new Dictionary<Term, List<DocumentPosting>>();

            foreach (var doc in ReadSource())
            {
                doc.Id = index++;

                WriteDocument(doc);

                var analyzedDoc = _analyzer.AnalyzeDocument(doc);

                trieBuilders.Add(BuildTree(analyzedDoc));

                foreach (var field in doc.Fields)
                {
                    _docCountByField.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
                }

                foreach (var term in analyzedDoc.Terms)
                {
                    List<DocumentPosting> postings;

                    if (matrix.TryGetValue(term.Key, out postings))
                    {
                        postings.Add(new DocumentPosting(doc.Id, term.Value));
                    }
                    else
                    {
                        matrix.Add(term.Key, new[] { new DocumentPosting(doc.Id, term.Value) }.ToList());
                    }
                }
            }

            Task.WaitAll(trieBuilders.ToArray());

            var trieWriter = SerializeTries();
            var postingsWriter = SerializePostings(matrix);

            CreateIxInfo().Save(Path.Combine(_directory, _indexName + ".ix"));

            Task.WaitAll(trieWriter, postingsWriter);

            return _indexName;
        }

        private Task SerializePostings(Dictionary<Term, List<DocumentPosting>> matrix)
        {
            return Task.Run(() =>
            {
                using (var db = CreatePostingsDb())
                {
                    foreach (var term in matrix)
                    {
                        db.Add(term.Key, term.Value.ToArray());
                    }
                }
            });
        }

        private Task SerializeTries()
        {
            return Task.Run(() =>
            {
                using (var work = new TaskQueue<Tuple<string, LcrsTrie>>(Math.Max(_tries.Count - 1, 1), DoSerializeTrie))
                {
                    foreach (var t in _tries)
                    {
                        work.Enqueue(new Tuple<string, LcrsTrie>(t.Key, t.Value));
                    }
                }
            });
        }

        private void DoSerializeTrie(Tuple<string, LcrsTrie> trieEntry)
        {
            var key = trieEntry.Item1;
            var trie = trieEntry.Item2;
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}.tri", _indexName, key));
            trie.SerializeMapped(fileName);
        }

        private Task BuildTree(AnalyzedDocument analyzedDoc)
        {
            return Task.Run(() =>
            {
                foreach (var term in analyzedDoc.Terms)
                {
                    WriteToTrie(term.Key.Field, term.Key.Word.Value);
                }
            });
        }

        private void WriteDocument(Document doc)
        {
            _docWriter.Write(doc);
        }

        private void WriteToTrie(string field, string value)
        {
            if (field == null) throw new ArgumentNullException("field");
            if (value == null) throw new ArgumentNullException("value");

            var trie = GetTrie(field, value);
            trie.Add(value);
        }

        private LcrsTrie GetTrie(string field, string token)
        {
            var key = string.Format("{0}-{1}", field.ToTrieFileId(), token.ToBucketName());
            LcrsTrie trie;

            if (!_tries.TryGetValue(key, out trie))
            {
                lock (_sync)
                {
                    if (!_tries.TryGetValue(key, out trie))
                    {
                        trie = new LcrsTrie('\0', false);
                        _tries[key] = trie;
                    }
                }
            }
            return trie;
        }

        private BPlusTree<int, byte[]> CreateDocumentDb()
        {
            var dbOptions = new BPlusTree<int, byte[]>.OptionsV2(
                PrimitiveSerializer.Int32,
                PrimitiveSerializer.Bytes);

            dbOptions.FileName = Path.Combine(_directory, string.Format("{0}-{1}.{2}", _indexName, "doc", "db"));
            dbOptions.CreateFile = CreatePolicy.Always;
            dbOptions.LockingFactory = new IgnoreLockFactory();

            return new BPlusTree<int, byte[]>(dbOptions);
        }


        private BPlusTree<Term, DocumentPosting[]> CreatePostingsDb()
        {
            var dbOptions = new BPlusTree<Term, DocumentPosting[]>.OptionsV2(
                new TermSerializer(),
                new ArraySerializer<DocumentPosting>(new PostingSerializer()), new TermComparer());

            dbOptions.FileName = Path.Combine(_directory, string.Format("{0}-{1}.{2}", _indexName, "pos", "db"));
            dbOptions.CreateFile = CreatePolicy.Always;
            dbOptions.LockingFactory = new IgnoreLockFactory();

            return new BPlusTree<Term, DocumentPosting[]>(dbOptions);
        }

        private IxInfo CreateIxInfo()
        {
            return new IxInfo
            {
                Name = _indexName,
                DocumentCount = new DocumentCount(new Dictionary<string, int>(_docCountByField)),
                Deletions = new List<int>()
            };
        }

        public void Dispose()
        {
            _docWriter.Dispose();

        }
    }

}