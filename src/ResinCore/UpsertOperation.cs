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
        protected static readonly ILog Log = LogManager.GetLogger(typeof(UpsertOperation));

        protected readonly Dictionary<ulong, object> Pks;

        protected abstract IEnumerable<Document> ReadSource();

        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly Compression _compression;
        private readonly long _indexVersionId;

        protected UpsertOperation(string directory, IAnalyzer analyzer, Compression compression)
        {
            _directory = directory;
            _analyzer = analyzer;
            _compression = compression;
            _indexVersionId = Util.GetChronologicalFileId();

            Pks = new Dictionary<UInt64, object>();
        }

        public long Commit()
        {
            var docAddresses = new List<BlockInfo>();
            var ts = new List<Task>();
            var trieBuilder = new TrieBuilder();

            using (var documentsToStore = new BlockingCollection<Document>())
            using (var documentsToAnalyze = new BlockingCollection<Document>())
            {
                ts.Add(Task.Run(() =>
                {
                    Log.Info("reading documents");

                    var readTimer = new Stopwatch();
                    readTimer.Start();

                    var count = 0;

                    foreach (var doc in ReadSource())
                    {
                        documentsToAnalyze.Add(doc);
                        documentsToStore.Add(doc);

                        count++;
                    }
                    documentsToAnalyze.CompleteAdding();
                    documentsToStore.CompleteAdding();

                    Log.InfoFormat("read {0} documents in {1}", count, readTimer.Elapsed);

                }));

                ts.Add(Task.Run(() =>
                {
                    var analyzeTimer = new Stopwatch();
                    analyzeTimer.Start();

                    Log.Info("analyzing");

                    var count = 0;

                    try
                    {
                        while (true)
                        {
                            var doc = documentsToAnalyze.Take();
                            var analyzed = _analyzer.AnalyzeDocument(doc);

                            foreach (var term in analyzed.Words.GroupBy(t=>t.Term.Field))
                            {
                                trieBuilder.Add(term.Key, term.Select(t =>
                                {
                                    var field = t.Term.Field;
                                    var token = t.Term.Word.Value;
                                    var posting = t.Posting;
                                    return new WordInfo(field, token, posting);
                                }).ToList());
                            }

                            count++;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Done
                        trieBuilder.CompleteAdding();
                    }
                    Log.InfoFormat("analyzed {0} documents in {1}", count, analyzeTimer.Elapsed);

                }));
                
                ts.Add(Task.Run(() =>
                {
                    var docWriterTimer = new Stopwatch();
                    docWriterTimer.Start();

                    Log.Info("serializing documents");

                    var docFileName = Path.Combine(_directory, _indexVersionId + ".doc");
                    var count = 0;

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

                                count++;
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            // Done
                        }
                    }

                    Log.InfoFormat("serialized {0} documents in {1}", count, docWriterTimer.Elapsed);

                }));

                Task.WaitAll(ts.ToArray());
            }
            var tries = trieBuilder.GetTries();

            var tasks = new List<Task>
            {
                Task.Run(() =>
                {
                    var postingsTimer = new Stopwatch();
                    postingsTimer.Start();

                    Log.Info("serializing postings");

                    var posFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexVersionId, "pos"));
                    using (var postingsWriter = new PostingsWriter(new FileStream(posFileName, FileMode.Create, FileAccess.Write, FileShare.None)))
                    {
                        foreach (var trie in tries)
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

                    Log.InfoFormat("serialized postings in {0}", postingsTimer.Elapsed);
                }),
                Task.Run(() =>
                {
                    var trieTimer = new Stopwatch();
                    trieTimer.Start();

                    Log.Info("serializing tries");

                    SerializeTries(tries);

                    Log.InfoFormat("serialized tries in {0}", trieTimer.Elapsed);
                }),
                Task.Run(() =>
                {
                    var docAdrTimer = new Stopwatch();
                    docAdrTimer.Start();

                    Log.Info("serializing doc addresses");

                    using (var docAddressWriter = new DocumentAddressWriter(new FileStream(Path.Combine(_directory, _indexVersionId + ".da"), FileMode.Create, FileAccess.Write, FileShare.None)))
                    {
                        foreach (var address in docAddresses)
                        {
                            docAddressWriter.Write(address);
                        }
                    }

                     Log.InfoFormat("serialized doc addresses in {0}", docAdrTimer.Elapsed);
                }),
                Task.Run(() =>
                {
                    var docHasTimer = new Stopwatch();
                    docHasTimer.Start();

                    Log.Info("serializing doc hashes");

                    var docHashesFileName = Path.Combine(_directory, string.Format("{0}.{1}", _indexVersionId, "pk"));

                    Pks.Keys.Select(h=>new DocHash(h)).Serialize(docHashesFileName);

                    Log.InfoFormat("serialized doc hashes in {0}", docHasTimer.Elapsed);
                })
            };

            Task.WaitAll(tasks.ToArray());

            CreateIxInfo().Serialize(Path.Combine(_directory, _indexVersionId + ".ix"));

            if (_compression > 0)
            {
                Log.Info("compression: true");
            }
            else
            {
                Log.Info("compression: false");
            }

            return _indexVersionId;
        }

        private void SerializeTries(IDictionary<string, LcrsTrie> tries)
        {
            Parallel.ForEach(tries, t => DoSerializeTrie(new Tuple<string, LcrsTrie>(t.Key, t.Value)));
        }

        private void DoSerializeTrie(Tuple<string, LcrsTrie> trieEntry)
        {
            var key = trieEntry.Item1;
            var trie = trieEntry.Item2;
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}.tri", _indexVersionId, key));

            trie.Serialize(fileName);
        }

        private IxInfo CreateIxInfo()
        {
            return new IxInfo
            {
                VersionId = _indexVersionId,
                DocumentCount = Pks.Count,
                Compression = _compression
            };
        }
    }
}