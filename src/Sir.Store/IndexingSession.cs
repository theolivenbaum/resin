using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Sir.Core;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class IndexingSession : CollectionSession
    {
        private readonly ValueWriter _vals;
        private readonly ValueWriter _keys;
        private readonly DocWriter _docs;
        private readonly ValueIndexWriter _valIx;
        private readonly ValueIndexWriter _keyIx;
        private readonly DocIndexWriter _docIx;
        private readonly RemotePostingsWriter _postingsWriter;
        private readonly Stopwatch _timer;
        private readonly ProducerConsumerQueue<(long keyId, VectorNode index, IEnumerable<(ulong, string)> tokens)> _buildQueue;
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;
        private readonly Dictionary<long, VectorNode> _dirty;
        private bool _completed;

        public IndexingSession(
            string collectionId, 
            LocalStorageSessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationService config) : base(collectionId, sessionFactory)
        {
            _tokenizer = tokenizer;
            _log = Logging.CreateWriter("session");
            _buildQueue = new ProducerConsumerQueue<(long keyId, VectorNode index, IEnumerable<(ulong, string)> tokens)>(Build);
            _dirty = new Dictionary<long, VectorNode>();

            ValueStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", collectionId)));
            KeyStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", collectionId)));
            DocStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", collectionId)));
            ValueIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", collectionId)));
            KeyIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", collectionId)));
            DocIndexStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", collectionId)));
            //PostingsStream = sessionFactory.CreateReadWriteStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.pos", collectionId)));
            VectorStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vec", collectionId)));
            Index = sessionFactory.GetCollectionIndex(collectionId.ToHash());

            _vals = new ValueWriter(ValueStream);
            _keys = new ValueWriter(KeyStream);
            _docs = new DocWriter(DocStream);
            _valIx = new ValueIndexWriter(ValueIndexStream);
            _keyIx = new ValueIndexWriter(KeyIndexStream);
            _docIx = new DocIndexWriter(DocIndexStream);
            _postingsWriter = new RemotePostingsWriter(config);
            _timer = new Stopwatch();
        }

        private void Write(IDictionary doc, Dictionary<long, HashSet<(ulong docId, string token)>> columns)
        {
            var docId = (ulong)doc["__docid"];

            foreach (var obj in doc.Keys)
            {
                var key = (string)obj;

                if (key.StartsWith("__"))
                    continue;

                var keyHash = key.ToHash();
                var keyId = SessionFactory.GetKeyId(keyHash);

                HashSet<(ulong docId, string token)> column;

                if (!columns.TryGetValue(keyId, out column))
                {
                    column = new HashSet<(ulong docId, string token)>(new TokenComparer());
                    columns.Add(keyId, column);
                }

                var val = (IComparable)doc[obj];
                var str = val as string;

                if (str == null || key[0] == '_')
                {
                    var v = val.ToString();

                    if (!string.IsNullOrWhiteSpace(v))
                    {
                        column.Add((docId, v));
                    }
                }
                else
                {
                    var tokens = _tokenizer.Tokenize(str);

                    foreach (var token in tokens)
                    {
                        column.Add((docId, token));
                    }
                }
            }
        }

        public void Write(IDictionary doc)
        {
            try
            {
                var columns = new Dictionary<long, HashSet<(ulong docId, string token)>>();

                Write(doc, columns);

                foreach (var column in columns)
                {
                    var keyId = column.Key;

                    VectorNode ix;
                    if (!_dirty.TryGetValue(keyId, out ix))
                    {
                        ix = GetIndex(keyId);

                        if (ix == null)
                        {
                            ix = new VectorNode();
                            //SessionFactory.AddIndex(CollectionId, keyId, ix);
                        }
                        _dirty.Add(keyId, ix);
                    }
                }

                Parallel.ForEach(columns, column =>
                {
                    var keyId = column.Key;
                    var tokens = column.Value;
                    var ix = _dirty[keyId];

                    Build(keyId, ix, tokens);
                });
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        public void Write(AnalyzeJob job)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                var docCount = 0;
                var columns = new Dictionary<long, HashSet<(ulong docId, string token)>>();

                foreach(var doc in job.Documents)
                {
                    docCount++;

                    Write(doc, columns);
                }

                _log.Log(string.Format("analyzed {0} docs in {1}", docCount, timer.Elapsed));

                timer.Restart();

                foreach (var column in columns)
                {
                    var keyId = column.Key;

                    VectorNode ix;
                    if (!_dirty.TryGetValue(keyId, out ix))
                    {
                        ix = GetIndex(keyId);

                        if (ix == null)
                        {
                            ix = new VectorNode();
                            //SessionFactory.AddIndex(CollectionId, keyId, ix);
                        }
                        _dirty.Add(keyId, ix);
                    }
                }

                foreach(var column in columns)
                {
                    var keyId = column.Key;
                    var tokens = column.Value;
                    var ix = _dirty[keyId];

                    Build(keyId, ix, tokens);
                }
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        private void Build((long keyId, VectorNode index, IEnumerable<(ulong docId, string token)> tokens) job)
        {
            Build(job.keyId, job.index, job.tokens);
        }

        private void Build(long keyId, VectorNode index, IEnumerable<(ulong docId, string token)> tokens)
        {
            var timer = new Stopwatch();
            timer.Start();

            var count = 0;

            foreach (var token in tokens)
            {
                index.Add(new VectorNode(token.token, token.docId));
                count++;
            }

            _log.Log(string.Format("added {0} nodes to column {1}.{2} in {3}. {4}",
                count, CollectionId, keyId, timer.Elapsed, index.Size()));
        }

        private void Serialize(long keyId, VectorNode node)
        {
            try
            {
                using (var ixFile = CreateIndexStream(keyId))
                {
                    node.SerializeTreeAndPayload(
                        CollectionId,
                        ixFile,
                        VectorStream,
                        _postingsWriter);
                }
                _log.Log(string.Format("serialized column {0}", keyId));
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        private Stream CreateIndexStream(long keyId)
        {
            var fileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId, keyId));
            return new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        public void Serialize()
        {
            if (_completed)
                return;

            try
            {
                _buildQueue.Dispose();

                _log.Log("build queue completed.");

                foreach (var x in _dirty)
                {
                    Serialize(x.Key, x.Value);
                }

                _log.Log("serialization completed.");

                _completed = true;
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        public override void Dispose()
        {
            if (!_completed)
            {
                Serialize();
                _completed = true;
            }

            base.Dispose();
        }
    }

    internal class TokenComparer : IEqualityComparer<(ulong, string)>
    {
        public bool Equals((ulong, string) x, (ulong, string) y)
        {
            if (ReferenceEquals(x, y)) return true;

            return x.Item1 == y.Item1 && x.Item2 == y.Item2;
        }

        public int GetHashCode((ulong, string) obj)
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                hash = hash * 23 + obj.Item1.GetHashCode();
                hash = hash * 23 + obj.Item2.GetHashCode();
                return hash;
            }
        }
    }
}