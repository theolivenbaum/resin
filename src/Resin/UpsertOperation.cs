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
    public abstract class UpsertOperation
    {
        protected abstract IEnumerable<Document> ReadSource();

        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly bool _compression;
        private readonly string _primaryKey;
        private readonly string _indexVersionId;
        private readonly Dictionary<string, LcrsTrie> _tries;
        private readonly ConcurrentDictionary<string, int> _docCountByField;
        private readonly int _startDocId;
        private readonly List<IxInfo> _ixs;
        
        private int _docId;

        protected UpsertOperation(string directory, IAnalyzer analyzer, bool compression, string primaryKey)
        {
            _directory = directory;
            _analyzer = analyzer;
            _compression = compression;
            _indexVersionId = Util.GetChronologicalFileId();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCountByField = new ConcurrentDictionary<string, int>();
            _ixs = Util.GetIndexFileNamesInChronologicalOrder(directory).Select(IxInfo.Load).ToList();
            _docId = _ixs.Count == 0 ? 0 : _ixs.OrderByDescending(x => x.NextDocId).First().NextDocId;
            _startDocId = _docId;
            _primaryKey = _ixs.Count == 0 ? null : primaryKey;
        }

        public string Write()
        {
            var docAddresses = new List<BlockInfo>();

            var primaryKeyValues = new List<Word>();
            var primaryKeyColumn = new LcrsTrie('\0', false);
            var latestIndexVersionId = string.Empty;

            if (_primaryKey != null)
            {
                latestIndexVersionId = _ixs.OrderBy(x => x.VersionId).Last().VersionId;

                primaryKeyColumn = Serializer.DeserializeTrie(_directory, latestIndexVersionId, _primaryKey);
            }
            
            // producer/consumer: https://msdn.microsoft.com/en-us/library/dd267312.aspx

            using (var analyzedDocuments = new BlockingCollection<AnalyzedDocument>())
            {
                using (Task producer = Task.Factory.StartNew(() =>
                {
                    var docFileName = Path.Combine(_directory, _indexVersionId + ".doc");

                    // Produce
                    using (var docWriter = new DocumentWriter(
                        new FileStream(docFileName, FileMode.Create, FileAccess.Write, FileShare.None), _compression))
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

                            if (_primaryKey != null)
                            {
                                var word = doc.Fields[_primaryKey];

                                Word found;

                                if (primaryKeyColumn.HasWord(word, out found))
                                {
                                    primaryKeyValues.Add(found);
                                }
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
                                    var field = term.Key.Field;
                                    var token = term.Key.Word.Value;
                                    var posting = term.Value;

                                    GetTrie(field, token).Add(token, posting);
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

            var tasks = new List<Task>
            {
                Task.Run(() =>
                {
                    var posFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexVersionId, "pos"));
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

            tasks.Add(
                Task.Run(() =>
                {
                    using (var docAddressWriter = new DocumentAddressWriter(new FileStream(Path.Combine(_directory, _indexVersionId + ".da"), FileMode.Create, FileAccess.Write, FileShare.None)))
                    {
                        foreach (var address in docAddresses)
                        {
                            docAddressWriter.Write(address);
                        }
                    }                    
            }));

            var latestDelFileName = Path.Combine(_directory, string.Format("{0}.del", latestIndexVersionId));
            var deletions = new LcrsTrie('\0', false);

            if (File.Exists(latestDelFileName))
            {
                deletions = Serializer.DeserializeTrie(latestDelFileName);

                if (primaryKeyValues.Count > 0)
                {
                    foreach (var word in primaryKeyValues)
                    {
                        deletions.Add(word.Value);
                    }
                }
            }

            var delFileName = Path.Combine(_directory, string.Format("{0}.del", _indexVersionId));

            deletions.Serialize(delFileName);  

            Task.WaitAll(tasks.ToArray());

            CreateIxInfo().Serialize(Path.Combine(_directory, _indexVersionId + ".ix"));

            return _indexVersionId;
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
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}.tri", _indexVersionId, key));
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
                VersionId = _indexVersionId,
                DocumentCount = new Dictionary<string, int>(_docCountByField),
                StartDocId = _startDocId,
                NextDocId = _docId
            };
        }
    }

}