using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sir.Core;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class IndexingSession : CollectionSession
    {
        private readonly RemotePostingsWriter _postingsWriter;
        //private readonly ProducerConsumerQueue<(long keyId, VectorNode index, IEnumerable<(ulong, string)> tokens)> _buildQueue;
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;
        private readonly Dictionary<long, VectorNode> _dirty;
        private bool _serialized;

        public IndexingSession(
            string collectionId, 
            LocalStorageSessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationService config) : base(collectionId, sessionFactory)
        {
            _tokenizer = tokenizer;
            _log = Logging.CreateWriter("indexingsession");
            //_buildQueue = new ProducerConsumerQueue<(long keyId, VectorNode index, IEnumerable<(ulong, string)> tokens)>(Build);
            _dirty = new Dictionary<long, VectorNode>();
            _postingsWriter = new RemotePostingsWriter(config);

            var collection = collectionId.ToHash();

            VectorStream = sessionFactory.CreateAppendStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vec", collection)));
            Index = sessionFactory.GetCollectionIndex(collectionId.ToHash());
        }

        public void Write(AnalyzeJob job)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                var docCount = 0;
                var columns = new Dictionary<long, HashSet<(ulong docId, string token)>>();

                foreach (var doc in job.Documents)
                {
                    docCount++;

                    Analyze(doc, columns);
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
                        }
                        _dirty.Add(keyId, ix);
                    }
                }

                //Parallel.ForEach(columns, column =>
                foreach(var column in columns)
                {
                    var keyId = column.Key;
                    var tokens = column.Value;
                    var ix = _dirty[keyId];

                    BuildInMemoryIndex(keyId, ix, tokens);

                    // validate
                    //File.WriteAllText(
                    //    Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.validate", CollectionId.ToHash(), keyId)),
                    //    string.Join('\n', tokens.Select(s=>s.token)));

                    //foreach (var token in tokens)
                    //{
                    //    var closestMatch = ix.ClosestMatch(new VectorNode(token.token), skipDirtyNodes:false);

                    //    if (closestMatch.Highscore < VectorNode.IdenticalAngle)
                    //    {
                    //        throw new DataMisalignedException();
                    //    }
                    //}
                }
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        private void Analyze(IDictionary doc, Dictionary<long, HashSet<(ulong docId, string token)>> columns)
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

        //private void Build((long keyId, VectorNode index, IEnumerable<(ulong docId, string token)> tokens) job)
        //{
        //    Build(job.keyId, job.index, job.tokens);
        //}

        private void BuildInMemoryIndex(long keyId, VectorNode index, IEnumerable<(ulong docId, string token)> tokens)
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

        public async Task Serialize()
        {
            if (_serialized)
                return;

            //_buildQueue.Dispose();

            var rootNodes = _dirty.ToList();

            await _postingsWriter.Write(CollectionId, rootNodes);

            foreach (var node in rootNodes)
            {
                using (var ixFile = CreateIndexStream(node.Key))
                {
                    node.Value.SerializeTree(CollectionId, ixFile, VectorStream);
                }
            }

            _log.Log("serialization complete.");

            _serialized = true;
        }

        private Stream CreateIndexStream(long keyId)
        {
            var fileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId.ToHash(), keyId));
            return new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        public override void Dispose()
        {
            if (!_serialized)
            {
                Task.Run(() => Serialize()).Wait();
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