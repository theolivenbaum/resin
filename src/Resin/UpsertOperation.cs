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
        private readonly long _indexVersionId;
        private readonly Dictionary<string, LcrsTrie> _tries;
        private readonly ConcurrentDictionary<string, int> _docCountByField;
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
            _docId = 0;
            _primaryKey = primaryKey;
        }

        public long Write()
        {
            var docAddresses = new List<BlockInfo>();
            var docHashes = new List<UInt32>();
            var primaryKeyValues = new List<Word>();
            var primaryKeyColumn = new LcrsTrie('\0', false);

            if (_ixs.Count > 0)
            {
                foreach (var ix in _ixs)
                {
                    var trie = Serializer.DeserializeTrie(_directory, ix.VersionId, _primaryKey);
                    primaryKeyColumn.Append(trie);
                }
            }
            
            // producer/consumer acc. to: https://msdn.microsoft.com/en-us/library/dd267312.aspx

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

                            docHashes.Add(doc.Fields[_primaryKey].ToHash());

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
                }),
                Task.Run(() =>
                {
                    using (var docAddressWriter = new DocumentAddressWriter(new FileStream(Path.Combine(_directory, _indexVersionId + ".da"), FileMode.Create, FileAccess.Write, FileShare.None)))
                    {
                        foreach (var address in docAddresses)
                        {
                            docAddressWriter.Write(address);
                        }
                    }
                })
                ,
                Task.Run(() =>
                {
                    var docHashesFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexVersionId, "dhs"));

                    docHashes.Serialize(docHashesFileName);
                })
            };

            if (_ixs.Count > 0)
            {
                var deletions = new LcrsTrie('\0', false);

                foreach (var word in primaryKeyValues)
                {
                    deletions.Add(word.Value);
                }

                var latestVersion = _ixs.OrderBy(x => x.VersionId).Last();
                var delFileName = Path.Combine(_directory, string.Format("{0}.del", latestVersion.VersionId));

                deletions.Serialize(delFileName);
            }

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
                DocumentCount = new Dictionary<string, int>(_docCountByField)
            };
        }
    }

}