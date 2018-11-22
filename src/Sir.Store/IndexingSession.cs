using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class IndexingSession : CollectionSession
    {
        private readonly RemotePostingsWriter _postingsWriter;
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
            _dirty = new Dictionary<long, VectorNode>();
            _postingsWriter = new RemotePostingsWriter(config);

            var collection = collectionId.ToHash();

            Index = sessionFactory.GetCollectionIndex(collectionId.ToHash());
        }

        public async Task Write(IndexingJob job)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                var docCount = 0;
                var analyzed = new Dictionary<long, HashSet<(ulong docId, string token)>>();

                // analyze
                foreach (var doc in job.Documents)
                {
                    docCount++;

                    Analyze(doc, analyzed);
                }

                _log.Log(string.Format("analyzed {0} docs in {1}", docCount, timer.Elapsed));

                timer.Restart();

                foreach (var column in analyzed)
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

                foreach (var column in analyzed)
                {
                    var keyId = column.Key;
                    var tokens = column.Value;
                    var ix = _dirty[keyId];

                    await BuildInMemoryIndex(keyId, ix, tokens);

                    // validate
                    foreach (var token in tokens)
                    {
                        var closestMatch = ix.ClosestMatch(new VectorNode(token.token), skipDirtyNodes: false);

                        if (closestMatch.Highscore < VectorNode.IdenticalAngle)
                        {
                            throw new DataMisalignedException();
                        }
                    }
                    File.WriteAllText(
                        Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.validate", CollectionId.ToHash(), keyId)),
                        string.Join('\n', tokens.Select(s => s.token)));

                }

                await Serialize();

                _log.Log(string.Format("built index in {0}", timer.Elapsed));

            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        private void HandleBuildError(Exception ex)
        {
            _log.Log(ex);

            throw ex;
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

        private async Task BuildInMemoryIndex(long keyId, VectorNode index, IEnumerable<(ulong docId, string token)> tokens)
        {
            var timer = new Stopwatch();
            timer.Start();

            var count = 0;
            using (var vectorStream = SessionFactory.CreateAppendStream(
                Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.vec", CollectionId.ToHash(), keyId))))
            {
                foreach (var token in tokens)
                {
                    await index.Add(new VectorNode(token.token, token.docId), vectorStream);
                    count++;
                }
            }

            _log.Log(string.Format("added {0} nodes to column {1}.{2} in {3}. {4}",
                count, CollectionId, keyId, timer.Elapsed, index.Size()));
        }

        private async Task Serialize()
        {
            if (_serialized)
                return;

            var rootNodes = _dirty.ToList();

            await _postingsWriter.Write(CollectionId, rootNodes);

            foreach (var node in rootNodes)
            {
                using (var ixFile = CreateIndexStream(node.Key))
                {
                    await node.Value.SerializeTree(ixFile);
                }
            }

            _serialized = true;
        }

        private Stream CreateIndexStream(long keyId)
        {
            var fileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId.ToHash(), keyId));
            return new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
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