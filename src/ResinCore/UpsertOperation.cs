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
        private readonly Compression _compression;
        private readonly string _primaryKey;
        private readonly long _indexVersionId;
        private readonly Dictionary<string, LcrsTrie> _tries;
        
        private int _docCount;
        private readonly bool _autoGeneratePk;

        protected UpsertOperation(string directory, IAnalyzer analyzer, Compression compression, string primaryKey)
        {
            _directory = directory;
            _analyzer = analyzer;
            _compression = compression;
            _indexVersionId = Util.GetChronologicalFileId();
            _tries = new Dictionary<string, LcrsTrie>();
            _docCount = 0;
            _primaryKey = primaryKey;
            _autoGeneratePk = string.IsNullOrWhiteSpace(_primaryKey);
        }

        public long Commit()
        {
            var docAddresses = new List<BlockInfo>();
            var pks = new Dictionary<UInt64, object>();
            var ts = new List<Task>();

            using (var documentsToStore = new BlockingCollection<Document>())
            using (var documentsToAnalyze = new BlockingCollection<Document>())
            using (var words = new BlockingCollection<List<WordInfo>>())
            {
                ts.Add(Task.Run(() =>
                {
                    var count = 0;
                    var readTimer = new Stopwatch();
                    readTimer.Start();

                    foreach (var doc in ReadSource())
                    {
                        string pkVal;

                        if (_autoGeneratePk)
                        {
                            pkVal = Guid.NewGuid().ToString();
                        }
                        else
                        {
                            pkVal = doc.Fields.First(f => f.Key == _primaryKey).Value;
                        }

                        var hash = pkVal.ToHash();

                        if (pks.ContainsKey(hash))
                        {
                            Log.WarnFormat("Found multiple occurrences of documents with pk value of {0} (id:{1}). Only first occurrence will be stored.",
                                pkVal, _docCount);
                        }
                        else
                        {
                            pks.Add(hash, null);

                            doc.Id = count++;

                            documentsToAnalyze.Add(doc);
                            documentsToStore.Add(doc);
                        }
                    }
                    documentsToAnalyze.CompleteAdding();
                    documentsToStore.CompleteAdding();

                    _docCount = count;

                    Log.InfoFormat("Read {0} documents in {1}", _docCount, readTimer.Elapsed);

                }));

                ts.Add(Task.Run(() =>
                {
                    var analyzeTimer = new Stopwatch();
                    analyzeTimer.Start();

                    try
                    {
                        while (true)
                        {
                            var doc = documentsToAnalyze.Take();
                            var analyzed = _analyzer.AnalyzeDocument(doc);
                            var docWords = new List<WordInfo>();

                            foreach (var term in analyzed.Words)
                            {
                                var field = term.Term.Field;
                                var token = term.Term.Word.Value;
                                var posting = term.Posting;

                                docWords.Add(new WordInfo(field, token, posting));
                            }

                            words.Add(docWords);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Done
                        words.CompleteAdding();
                    }
                    Log.InfoFormat("Analyzed {0} documents in {1}", pks.Count, analyzeTimer.Elapsed);

                }));
                ts.Add(Task.Run(() =>
                {
                    var trieTimer = new Stopwatch();
                    trieTimer.Start();

                    try
                    {
                        while (true)
                        {
                            var docWords = words.Take();

                            foreach(var word in docWords)
                            {
                                GetTrie(word.Field)
                                .Add(word.Token, word.Posting);
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Done
                    }

                    Log.InfoFormat("Built tries in {0}", trieTimer.Elapsed);
                }));

                ts.Add(Task.Run(() =>
                {
                    var docWriterTimer = new Stopwatch();
                    docWriterTimer.Start();

                var docFileName = Path.Combine(_directory, _indexVersionId + ".doc");

                using (var docWriter = new DocumentWriter(
                    new FileStream(docFileName, FileMode.Create, FileAccess.Write, FileShare.None), _compression))
                {
                    try
                    {
                        while (true)
                        {
                            var doc = documentsToStore.Take();

                            var adr = docWriter.Write(doc);

                            docAddresses.Add(adr);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Done
                    }
                }

                Log.InfoFormat("Serialized {0} documents in {1}", _docCount, docWriterTimer.Elapsed);

                }));

                Task.WaitAll(ts.ToArray());
            }

            if (pks.Count == 0)
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
                                node.PostingsAddress = postingsWriter.Write(node.Postings);
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

                    pks.Keys.Select(h=>new DocHash(h)).Serialize(docHashesFileName);

                    Log.InfoFormat("Serialized doc hashes in {0}", docHasTimer.Elapsed);
                })
            };

            Task.WaitAll(tasks.ToArray());

            CreateIxInfo().Serialize(Path.Combine(_directory, _indexVersionId + ".ix"));

            if(_compression > 0) Log.Info("compression: true");

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

        private LcrsTrie GetTrie(string field)
        {
            var key = field.ToHash().ToString();

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
                DocumentCount = _docCount,
                Compression = _compression
            };
        }
    }
}