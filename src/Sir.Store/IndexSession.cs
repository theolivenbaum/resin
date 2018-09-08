using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Sir.Core;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class IndexSession : CollectionSession
    {
        private readonly ValueWriter _vals;
        private readonly ValueWriter _keys;
        private readonly DocWriter _docs;
        private readonly ValueIndexWriter _valIx;
        private readonly ValueIndexWriter _keyIx;
        private readonly DocIndexWriter _docIx;
        private readonly PagedPostingsReader _postingsReader;
        private readonly Dictionary<long, VectorNode> _dirty;
        private readonly Stopwatch _timer;
        private readonly ProducerConsumerQueue<AnalyzeJob> _analyzeQueue;
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;
        private int _docCount;

        public IndexSession(
            ulong collectionId, 
            LocalStorageSessionFactory sessionFactory, 
            ITokenizer tokenizer) : base(collectionId, sessionFactory)
        {
            _tokenizer = tokenizer;
            _log = Logging.CreateWriter("session");
            _analyzeQueue = new ProducerConsumerQueue<AnalyzeJob>(Consume);

            ValueStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collectionId)));
            KeyStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collectionId)));
            DocStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collectionId)));
            ValueIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collectionId)));
            KeyIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collectionId)));
            DocIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collectionId)));
            //PostingsStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.pos", collectionId)));
            //VectorStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vec", collectionId)));
            Index = sessionFactory.GetCollectionIndex(collectionId);

            _vals = new ValueWriter(ValueStream);
            _keys = new ValueWriter(KeyStream);
            _docs = new DocWriter(DocStream);
            _valIx = new ValueIndexWriter(ValueIndexStream);
            _keyIx = new ValueIndexWriter(KeyIndexStream);
            _docIx = new DocIndexWriter(DocIndexStream);
            _postingsReader = new PagedPostingsReader(PostingsStream);
            _dirty = new Dictionary<long, VectorNode>();
            _timer = new Stopwatch();
        }

        public void Write(AnalyzeJob job)
        {
            _analyzeQueue.Enqueue(job);
        }

        private void Consume(AnalyzeJob job)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                var docCount = 0;

                foreach (var doc in job.Documents)
                {
                    var docId = (ulong)doc["__docid"];

                    var keys = doc.Keys
                        .Cast<string>()
                        .Where(x => !x.StartsWith("__"));

                    foreach (var key in keys)
                    {
                        var keyHash = key.ToHash();
                        var keyId = SessionFactory.GetKeyId(keyHash);
                        VectorNode ix;

                        if (!_dirty.TryGetValue(keyId, out ix))
                        {
                            ix = GetIndex(keyHash) ?? new VectorNode();
                            _dirty.Add(keyId, ix);
                        }

                        var val = (IComparable)doc[key];
                        var str = val as string;

                        if (str == null || key[0] == '_')
                        {
                            var v = val.ToString();

                            if (!string.IsNullOrWhiteSpace(v))
                            {
                                ix.Add(new VectorNode(v, docId));
                            }
                        }
                        else
                        {
                            foreach (var x in _tokenizer.Tokenize(str))
                            {
                                if (!string.IsNullOrWhiteSpace(x))
                                {
                                    ix.Add(new VectorNode(x, docId));
                                }
                            }
                        }
                    }

                    if (++docCount == 1000)
                    {
                        _log.Log(string.Format("analyzed doc {0}", doc["__docid"]));
                        docCount = 0;
                    }
                }

                _log.Log(string.Format("executed {0} analyze job in {1}", job.CollectionId, timer.Elapsed));
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }
        private void Consume(BuildJob job)
        {
            _timer.Restart();

            foreach (var kvp in job.Tokens)
            {
                var ix = _dirty[kvp.Key];

                foreach (var token in kvp.Value)
                {
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        var node = new VectorNode(token, job.DocId);

                        ix.Add(node);
                    }
                }
            }

            if (++_docCount == 1000)
            {
                _log.Log(string.Format("processed doc {0} in {1}", job.DocId, _timer.Elapsed));
                _docCount = 0;
            }
        }

        public void FlushToMemory()
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                _analyzeQueue.Dispose();

                _log.Log("finished analyzing.");

                if (_dirty.Count > 0)
                {
                    _log.Log(string.Format("loading {0} indexes", _dirty.Count));
                }

                foreach (var node in _dirty)
                {
                    var keyId = node.Key;

                    SessionFactory.AddIndex(CollectionId, keyId, node.Value);

                    _log.Log(string.Format("refreshed index {0}.{1}", CollectionId, keyId));
                }

                if (_dirty.Count > 0)
                {
                    _log.Log(string.Format("loaded {0} indexes", _dirty.Count));
                }

                _flushed = true;

                _log.Log(string.Format("flushing took {0}", timer.Elapsed));
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        private bool _flushed;

        public override void Dispose()
        {
            if (!_flushed)
            {
                FlushToMemory();
                _flushed = true;
            }

            base.Dispose();
        }

        private class BuildJob
        {
            public ulong DocId { get; }
            public IDictionary<long, HashSet<string>> Tokens { get; }

            public BuildJob(ulong docId, IDictionary<long, HashSet<string>> tokens)
            {
                DocId = docId;
                Tokens = tokens;
            }
        }
    }
}