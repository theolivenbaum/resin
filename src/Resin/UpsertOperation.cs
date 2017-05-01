using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Write;
using Resin.Sys;
using System.Diagnostics;

namespace Resin
{
    public abstract class UpsertOperation
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UpsertOperation));

        protected abstract IEnumerable<Document> ReadSource();

        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly bool _compression;
        private readonly string _primaryKey;
        private readonly long _indexVersionId;
        private readonly Dictionary<string, LcrsTrie> _tries;
        
        private int _docId;
        private readonly bool _autoGeneratePk;

        protected UpsertOperation(string directory, IAnalyzer analyzer, bool compression, string primaryKey)
        {
            _directory = directory;
            _analyzer = analyzer;
            _compression = compression;
            _indexVersionId = Util.GetChronologicalFileId();
            _tries = new Dictionary<string, LcrsTrie>();
            _docId = 0;
            _primaryKey = primaryKey;
            _autoGeneratePk = string.IsNullOrWhiteSpace(_primaryKey);
        }

        public long Commit()
        {
            var analyzedTimer = new Stopwatch();
            analyzedTimer.Start();

            var docAddresses = new List<BlockInfo>();
            var primaryKeyValues = new List<UInt64>();
            var pks = new Dictionary<UInt64, object>();
          
            // producer/consumer acc. to: https://msdn.microsoft.com/en-us/library/dd267312.aspx

            using (var documents = new BlockingCollection<Document>())
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
                            string pkVal;

                            if (_autoGeneratePk)
                            {
                                pkVal = Guid.NewGuid().ToString();
                            }
                            else
                            {
                                pkVal = doc.Fields[_primaryKey];
                            }

                            var hash = pkVal.ToHash();

                            if (pks.ContainsKey(hash))
                            {
                                Log.InfoFormat("Found multiple occurrences of documents with {0}:{1}. Only first occurrence will be stored.",
                                    _primaryKey, pkVal);
                            }
                            else
                            {
                                primaryKeyValues.Add(hash);

                                doc.Id = _docId++;

                                docAddresses.Add(docWriter.Write(doc));

                                documents.Add(doc); 
                            }
                        }
                    }

                    // Signal no more work
                    documents.CompleteAdding();
                }))
                {
                    using (Task consumer = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            // Consume
                            while (true)
                            {
                                var doc = documents.Take();
                                var analyzed = _analyzer.AnalyzeDocument(doc);

                                foreach (var term in analyzed.Words)
                                {
                                    var field = term.Key.Field;
                                    var token = term.Key.Word.Value;
                                    var posting = term.Value;

                                    GetTrie(field, token)
                                        .Add(token, posting);
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

            Log.InfoFormat("Analyzed {0} documents in {1}", primaryKeyValues.Count, analyzedTimer.Elapsed);

            if (primaryKeyValues.Count == 0)
            {
                Log.Info("Aborted write (source is empty).");

                return 0;
            }

            var tasks = new List<Task>
            {
                Task.Run(() =>
                {
                    var postingsTimer = new Stopwatch();
                    postingsTimer.Start();

                    var posFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexVersionId, "pos"));
                    using (var postingsWriter = new PostingsWriter(new FileStream(posFileName, FileMode.Create, FileAccess.Write, FileShare.None)))
                    {
                        foreach (var trie in _tries)
                        {
                            foreach (var node in trie.Value.EndOfWordNodes())
                            {
                                var postings = node.Postings.ToList();

                                node.PostingsAddress = postingsWriter.Write(postings);
                            }

                            if (Log.IsDebugEnabled)
                            {
                                foreach(var word in trie.Value.Words())
                                {
                                    Log.Debug(word);
                                }
                            }
                        }
                    }
                    Log.InfoFormat("Serialized postings in {0}", postingsTimer.Elapsed);

                    var trieTimer = new Stopwatch();
                    trieTimer.Start();

                    SerializeTries();

                    Log.InfoFormat("Serialized tries in {0}", trieTimer.Elapsed);
                }),
                Task.Run(() =>
                {
                    var docAdrTimer = new Stopwatch();
                    docAdrTimer.Start();

                    using (var docAddressWriter = new DocumentAddressWriter(new FileStream(Path.Combine(_directory, _indexVersionId + ".da"), FileMode.Create, FileAccess.Write, FileShare.None)))
                    {
                        foreach (var address in docAddresses)
                        {
                            docAddressWriter.Write(address);
                        }
                    }

                     Log.InfoFormat("Serialized doc addresses in {0}", docAdrTimer.Elapsed);
                }),
                Task.Run(() =>
                {
                    var docHasTimer = new Stopwatch();
                    docHasTimer.Start();

                    var docHashesFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexVersionId, "pk"));

                    primaryKeyValues.Select(h=>new DocHash(h)).Serialize(docHashesFileName);

                    Log.InfoFormat("Serialized doc hashes in {0}", docHasTimer.Elapsed);
                })
            };

            Task.WaitAll(tasks.ToArray());

            CreateIxInfo().Serialize(Path.Combine(_directory, _indexVersionId + ".ix"));

            return _indexVersionId;
        }

        private void SerializeTries()
        {
            Parallel.ForEach(_tries, t => DoSerializeTrie(new Tuple<string, LcrsTrie>(t.Key, t.Value)));
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
            var key = string.Format("{0}-{1}", field.ToHash(), token.ToTokenBasedBucket());
            LcrsTrie trie;

            if (!_tries.TryGetValue(key, out trie))
            {
                trie = new LcrsTrie();
                _tries[key] = trie;
            }
            return trie;
        }

        private IxInfo CreateIxInfo()
        {
            return new IxInfo
            {
                VersionId = _indexVersionId,
                DocumentCount = _docId
            };
        }
    }
}