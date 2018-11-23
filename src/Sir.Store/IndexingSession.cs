using System;
using System.Collections;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<long, VectorNode> _dirty;
        private bool _serialized;
        private Stopwatch _timer;
        private bool _validate;

        public IndexingSession(
            string collectionId, 
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationService config) : base(collectionId, sessionFactory)
        {
            _tokenizer = tokenizer;
            _log = Logging.CreateWriter("indexingsession");
            _dirty = new ConcurrentDictionary<long, VectorNode>();
            _postingsWriter = new RemotePostingsWriter(config);
            _timer = new Stopwatch();
            _validate = config.Get("create_index_validation_files") == "true";

            Index = sessionFactory.GetCollectionIndex(collectionId.ToHash()) ?? new SortedList<long, VectorNode>();
        }

        public async Task Write(IndexingJob job)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                var docCount = 0;

                foreach (var doc in job.Documents)
                {
                    docCount++;

                    await Write(doc);
                }

                _log.Log(string.Format("build in-memory index from {0} docs in {1}", docCount, timer.Elapsed));

                await Serialize();

                var collectionId = CollectionId.ToHash();

                foreach (var index in _dirty)
                {
                    SessionFactory.Publish(collectionId, index.Key, index.Value);
                }

                _log.Log(string.Format("indexed {0} docs in {1}", docCount, timer.Elapsed));
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }
        private static object Sync = new object();

        private async Task Write(IDictionary document)
        {
            _timer.Restart();

            var analyzed = new Dictionary<long, HashSet<string>>();

            Analyze(document, analyzed);

            foreach (var column in analyzed)
            {
                var keyId = column.Key;

                VectorNode ix;
                if (!_dirty.TryGetValue(keyId, out ix))
                {
                    lock (Sync)
                    {
                        if (!_dirty.TryGetValue(keyId, out ix))
                        {
                            ix = GetIndex(keyId);

                            if (ix == null)
                            {
                                ix = new VectorNode();
                            }
                            _dirty.GetOrAdd(keyId, ix);
                        }
                    }
                }
            }

            foreach (var column in analyzed)
            {
                var keyId = column.Key;
                var tokens = column.Value;
                var ix = _dirty[keyId];
                var docId = ulong.Parse(document["__docid"].ToString());

                BuildInMemoryIndex(docId, keyId, ix, tokens);

                // validate
                if (_validate)
                {
                    foreach (var token in tokens)
                    {
                        var query = new VectorNode(token);
                        var closestMatch = ix.ClosestMatch(query, skipDirtyNodes: false);

                        if (closestMatch.Score < VectorNode.IdenticalAngle)
                        {
                            throw new DataMisalignedException();
                        }
                    }

                    await File.WriteAllTextAsync(
                        Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.{2}.validate", CollectionId.ToHash(), keyId, document["__docid"])),
                        string.Join('\n', tokens));
                }
            }

            _log.Log(string.Format("indexed doc ID {0} in {1}", document["__docid"], _timer.Elapsed));
        }

        private void Analyze(IDictionary doc, Dictionary<long, HashSet<string>> columns)
        {
            var docId = (ulong)doc["__docid"];

            foreach (var obj in doc.Keys)
            {
                var key = (string)obj;

                if (key.StartsWith("__"))
                    continue;

                var keyHash = key.ToHash();
                var keyId = SessionFactory.GetKeyId(keyHash);

                HashSet<string> column;

                if (!columns.TryGetValue(keyId, out column))
                {
                    column = new HashSet<string>();
                    columns.Add(keyId, column);
                }

                var val = (IComparable)doc[obj];
                var str = val as string;

                if (str == null || key[0] == '_')
                {
                    var v = val.ToString();

                    if (!string.IsNullOrWhiteSpace(v))
                    {
                        column.Add(v);
                    }
                }
                else
                {
                    var tokens = _tokenizer.Tokenize(str);

                    foreach (var token in tokens)
                    {
                        column.Add(token);
                    }
                }
            }
        }

        private void BuildInMemoryIndex(ulong docId, long keyId, VectorNode index, IEnumerable<string> tokens)
        {
            using (var vectorStream = SessionFactory.CreateAppendStream(
                Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.vec", CollectionId.ToHash(), keyId))))
            {
                foreach (var token in tokens)
                {
                    index.Add(new VectorNode(token, docId), vectorStream);
                }
            }
        }

        private async Task Serialize()
        {
            if (_serialized)
                return;

            var timer = new Stopwatch();
            timer.Start();

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

            _log.Log(string.Format("serialized index tree and postings in {0}", timer.Elapsed));
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