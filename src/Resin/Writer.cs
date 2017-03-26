using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        protected Writer(string directory, IAnalyzer analyzer)
        {
            _directory = directory;
            _analyzer = analyzer;
            
            _indexName = Util.GetChronologicalFileId();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();
        }

        public string Write()
        {
            var index = 0;
            var matrix = new Dictionary<Term, List<DocumentPosting>>();
            var docAddresses = new List<BlockInfo>();
            var analyzedDocs = new List<AnalyzedDocument>();

            using (var docWriter = new DocumentWriter(new FileStream(Path.Combine(_directory, _indexName + ".doc"), FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                foreach (var doc in ReadSource())
                {
                    doc.Id = index++;

                    docAddresses.Add(docWriter.Write(doc));

                    analyzedDocs.Add(_analyzer.AnalyzeDocument(doc));

                    foreach (var field in doc.Fields)
                    {
                        _docCountByField.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
                    }
                }
            }

            for (int docId = 0; docId < analyzedDocs.Count; docId++)
            {
                var analyzed = analyzedDocs[docId];

                foreach (var term in analyzed.Terms)
                {
                    List<DocumentPosting> postings;

                    if (matrix.TryGetValue(term.Key, out postings))
                    {
                        postings.Add(new DocumentPosting(docId, term.Value));
                    }
                    else
                    {
                        matrix.Add(term.Key, new[] { new DocumentPosting(docId, term.Value) }.ToList());
                    }
                }
            }

            using (var postingsWriter = new PostingsWriter(new FileStream(Path.Combine(_directory, _indexName + ".pos"), FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                foreach (var term in matrix)
                {
                    var postingsAddress = postingsWriter.Write(term.Value);

                    GetTrie(term.Key.Field, term.Key.Word.Value).Add(term.Key.Word.Value, postingsAddress);
                } 
            }

            using (var docAddressWriter = new DocumentAddressWriter(new FileStream(Path.Combine(_directory, _indexName + ".da"), FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                foreach (var address in docAddresses)
                {
                    docAddressWriter.Write(address);
                }
            }

            var trieWriter = SerializeTries();

            CreateIxInfo().Save(Path.Combine(_directory, _indexName + ".ix"));

            Task.WaitAll(trieWriter);

            return _indexName;
        }

        private Task SerializeTries()
        {
            return Task.Run(() =>
            {
                //using (var work = new TaskQueue<Tuple<string, LcrsTrie>>(Math.Max(_tries.Count - 1, 1), DoSerializeTrie))
                //{
                //    foreach (var t in _tries)
                //    {
                //        work.Enqueue(new Tuple<string, LcrsTrie>(t.Key, t.Value));
                //    }
                //}
                foreach (var t in _tries)
                {
                    DoSerializeTrie(new Tuple<string, LcrsTrie>(t.Key, t.Value));
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
        }
    }

}