using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Write;
using Resin.Sys;

namespace Resin
{
    public class WriteOperation : IDisposable
    {
        private readonly string _directory;
        private readonly IEnumerable<Document> _documents;
        private readonly bool _fileBased;
        private readonly StreamReader _reader;
        private readonly IAnalyzer _analyzer;
        private readonly int _take;
        private readonly Dictionary<string, DocumentWriter> _docWriters;
        private readonly string _indexName;
        private readonly Dictionary<string, LcrsTrie> _tries;
        private readonly object _sync = new object();
        private readonly ConcurrentDictionary<string, int> _docCountByField;

        public WriteOperation(string directory, IAnalyzer analyzer, IEnumerable<Document> documents)
        {
            _directory = directory;
            _documents = documents;
            _analyzer = analyzer;
            _fileBased = false;
            _docWriters = new Dictionary<string, DocumentWriter>();
            _indexName = Util.GetChronologicalFileId();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();
        }

        public WriteOperation(string directory, IAnalyzer analyzer, string jsonFileName, int take)
        {
            _directory = directory;
            _analyzer = analyzer;
            _take = take;
            _fileBased = true;
            _docWriters = new Dictionary<string, DocumentWriter>();
            _indexName = Util.GetChronologicalFileId();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();

            var fs = File.Open(jsonFileName, FileMode.Open, FileAccess.Read, FileShare.None);
            var bs = new BufferedStream(fs);
            _reader = new StreamReader(bs, Encoding.Unicode);
        }

        public string Execute()
        {
            var trieBuilders = new List<Task>();
            var postingsMatrix = new Dictionary<Term, List<DocumentPosting>>();
            var index = 0;

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

                    if (postingsMatrix.TryGetValue(term.Key, out postings))
                    {
                        postings.Add(new DocumentPosting(doc.Id, term.Value));
                    }
                    else
                    {
                        postingsMatrix.Add(term.Key, new List<DocumentPosting> { new DocumentPosting(doc.Id, term.Value) });
                    }
                }
            }

            var postingsWriter = SerializePostings(postingsMatrix);

            Task.WaitAll(trieBuilders.ToArray());

            var trieWriter = SerializeTries();

            CreateIxInfo().Save(Path.Combine(_directory, _indexName + ".ix"));

            Task.WaitAll(postingsWriter, trieWriter);

            return _indexName;
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
            var field = trieEntry.Item1;
            var trie = trieEntry.Item2;
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}.tri", _indexName, field.ToTrieFileId()));
            trie.SerializeToTextFile(fileName);
        }

        private Task SerializePostings(Dictionary<Term, List<DocumentPosting>> postingsMatrix)
        {
            return Task.Run(() =>
            {
                var byFileId = new Dictionary<string, Dictionary<Term, List<DocumentPosting>>>();
                foreach (var term in postingsMatrix)
                {
                    var fileId = term.Key.ToPostingsFileId();

                    Dictionary<Term, List<DocumentPosting>> postings;
                    if (!byFileId.TryGetValue(fileId, out postings))
                    {
                        postings = new Dictionary<Term, List<DocumentPosting>>();
                        byFileId.Add(fileId, postings);
                    }
                    postings.Add(term.Key, term.Value);

                }
                Parallel.ForEach(byFileId, file =>
                {
                    var fileName = Path.Combine(_directory, string.Format("{0}-{1}.pos", _indexName, file.Key));

                    using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var sw = new StreamWriter(fs, Encoding.Unicode))
                    using (var writer = new PostingsWriter(sw))
                    {
                        writer.Write(file.Value);
                    }
                });
            });
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

        private void WriteToTrie(string field, string value)
        {
            if (field == null) throw new ArgumentNullException("field");
            if (value == null) throw new ArgumentNullException("value");

            var trie = GetTrie(field);
            trie.Add(value);
        }

        private LcrsTrie GetTrie(string field)
        {
            LcrsTrie trie;
            if (!_tries.TryGetValue(field, out trie))
            {
                lock (_sync)
                {
                    if (!_tries.TryGetValue(field, out trie))
                    {
                        trie = new LcrsTrie('\0', false);
                        _tries[field] = trie;
                    }
                }
            }
            return trie;
        }

        private void WriteDocument(Document doc)
        {
            var fileId = doc.Id.ToString(CultureInfo.InvariantCulture).ToDocFileId();
            DocumentWriter writer;

            if (!_docWriters.TryGetValue(fileId, out writer))
            {
                lock (DocumentWriter.SyncRoot)
                {
                    if (!_docWriters.TryGetValue(fileId, out writer))
                    {
                        var fileName = Path.Combine(_directory, string.Format("{0}-{1}.doc", _indexName, fileId));
                        var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
                        var sr = new StreamWriter(fs, Encoding.Unicode);

                        writer = new DocumentWriter(sr);

                        _docWriters.Add(fileId, writer);
                    }
                }
            }
            writer.Write(doc);
        }

        private IEnumerable<Document> ReadSource()
        {
            if (_fileBased)
            {
                _reader.ReadLine();

                string line;
                var took = 0;

                while ((line = _reader.ReadLine()) != null)
                {
                    if (line[0] == ']') break;

                    if (took++ == _take) break;

                    var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(line.Substring(0, line.Length - 1));

                    yield return new Document(dic);
                }
            }
            else
            {
                foreach (var doc in _documents)
                {
                    yield return doc;
                }
            }
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
            if (_reader != null)
            {
                _reader.Close();
                _reader.Dispose();
            }

            foreach (var writer in _docWriters.Values)
            {
                writer.Dispose();
            }
        }
    }

    public class DeleteOperation : IDisposable
    {
        private readonly string _directory;
        private readonly IEnumerable<int> _documentIds;
        private readonly string _indexName;

        //TODO: replace with delete by term
        public DeleteOperation(string directory, IEnumerable<int> documentIds)
        {
            _directory = directory;
            _documentIds = documentIds;
            _indexName = Util.GetChronologicalFileId();
        }

        public void Execute()
        {
            var ix = new IxInfo
            {
                Name = _indexName,
                DocumentCount = new DocumentCount(new Dictionary<string, int>()),
                Deletions = _documentIds.ToList()
            };
            ix.Save(Path.Combine(_directory, ix.Name + ".ix"));
        }

        public void Dispose()
        {
        }
    }
}