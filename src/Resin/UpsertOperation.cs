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
    public abstract class UpsertOperation : IDisposable
    {
        protected abstract IEnumerable<Document> ReadSource();

        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly bool _compression;
        private readonly string _indexName;
        private readonly Dictionary<string, LcrsTrie> _tries;
        private readonly ConcurrentDictionary<string, int> _docCountByField;

        private int _docId;

        protected UpsertOperation(string directory, IAnalyzer analyzer, bool compression = false)
        {
            _directory = directory;
            _analyzer = analyzer;
            _compression = compression;

            _indexName = Util.GetChronologicalFileId();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();

            var ixs = Util.GetIndexFileNamesInChronologicalOrder(directory).Select(IxInfo.Load).ToList();

            _docId = ixs.Count == 0 ? 0 : ixs.OrderByDescending(x => x.NextDocId).First().NextDocId;
        }

        public string Write()
        {
            var docAddresses = new List<BlockInfo>();

            using (var analyzedDocuments = new BlockingCollection<AnalyzedDocument>())
            {
                using (Task producer = Task.Factory.StartNew(() =>
                {
                    var docFileName = Path.Combine(_directory, _indexName + ".doc");

                    // Produce
                    using (var docWriter = new DocumentWriter(
                        new FileStream(docFileName, FileMode.Create, FileAccess.Write, FileShare.None),
                        _compression))
                    {
                        foreach (var doc in ReadSource())
                        {
                            doc.Id = _docId++;

                            docAddresses.Add(docWriter.Write(doc));

                            analyzedDocuments.Add(_analyzer.AnalyzeDocument(doc));

                            foreach (var field in doc.Fields)
                            {
                                _docCountByField.AddOrUpdate(field.Key, 1, (s, count) => count + 1);
                            }
                        }
                    }

                    // Signal no more work
                    analyzedDocuments.CompleteAdding();
                }))
                {
                    using (Task consumer = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            // Consume
                            while (true)
                            {
                                var analyzed = analyzedDocuments.Take();

                                foreach (var term in analyzed.Terms)
                                {
                                    GetTrie(term.Key.Field, term.Key.Word.Value)
                                        .Add(term.Key.Word.Value, term.Value);
                                }
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            // Done
                        }
                    })) 
                    Task.WaitAll(producer, consumer);
                }
            }

            //if (_ix != null)
            //{
            //    // upsert

            //    using (var collector = new Collector(_directory, _ix))
            //    {
            //        foreach (var trie in _tries)
            //        {
            //            var field = trie.Key;

            //            foreach (var posting in trie.Value.Words().SelectMany(word => word.Postings).ToList())
            //            {
            //                var query = new QueryContext(field, posting)  
            //            }

            //        }
            //    }
            //}

            var tasks = new List<Task>
            {
                Task.Run(() =>
                {
                    var posFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexName, "pos"));
                    using (var postingsWriter = new PostingsWriter(new FileStream(posFileName, FileMode.Create, FileAccess.Write, FileShare.None)))
                    {
                        foreach (var trie in _tries)
                        {
                            foreach (var node in trie.Value.EndOfWordNodes())
                            {
                                node.PostingsAddress = postingsWriter.Write(node.Postings);
                            }
                        }
                    }
                    SerializeTries();
                })
            };

            using (var docAddressWriter = new DocumentAddressWriter(new FileStream(Path.Combine(_directory, _indexName + ".da"), FileMode.Create, FileAccess.Write, FileShare.None)))
            {
                foreach (var address in docAddresses)
                {
                    docAddressWriter.Write(address);
                }
            }

            CreateIxInfo().Serialize(Path.Combine(_directory, _indexName + ".ix"));

            Task.WaitAll(tasks.ToArray());

            return _indexName;
        }

        private void SerializeTries()
        {
            Parallel.ForEach(_tries, t =>
            {
                DoSerializeTrie(new Tuple<string, LcrsTrie>(t.Key, t.Value));
            });
        }

        private void DoSerializeTrie(Tuple<string, LcrsTrie> trieEntry)
        {
            var key = trieEntry.Item1;
            var trie = trieEntry.Item2;
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}.tri", _indexName, key));
            trie.Serialize(fileName);
        }

        private LcrsTrie GetTrie(string field, string token)
        {
            var key = string.Format("{0}-{1}", field.ToHashString(), token.ToTrieBucketName());
            LcrsTrie trie;

            if (!_tries.TryGetValue(key, out trie))
            {
                trie = new LcrsTrie('\0', false);
                _tries[key] = trie;
            }
            return trie;
        }

        private IxInfo CreateIxInfo()
        {
            return new IxInfo
            {
                VersionId = _indexName,
                DocumentCount = new Dictionary<string, int>(_docCountByField),
                NextDocId = _docId
            };
        }

        public void Dispose()
        {
        }
    }

}