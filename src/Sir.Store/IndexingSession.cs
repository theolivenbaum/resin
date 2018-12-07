using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class IndexingSession : CollectionSession
    {
        private readonly ITokenizer _tokenizer;
        private readonly StreamWriter _log;
        private bool _validate;
        private readonly ConcurrentDictionary<long, VectorNode> _dirty;

        public IndexingSession(
            string collectionId, 
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationService config) : base(collectionId, sessionFactory)
        {
            _tokenizer = tokenizer;
            _log = Logging.CreateWriter("indexingsession");
            _validate = config.Get("create_index_validation_files") == "true";
            _dirty = new ConcurrentDictionary<long, VectorNode>();

            Index = sessionFactory.GetOrAddCollectionIndex(collectionId.ToHash());
        }

        public void Write(IndexingJob job)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                var docCount = 0;

                using (var vectorStream = SessionFactory.CreateAppendStream(
                    Path.Combine(SessionFactory.Dir, string.Format("{0}.vec", CollectionId.ToHash()))))
                {
                    foreach(var doc in job.Documents)
                    {
                        docCount++;

                        Write(doc, vectorStream);
                    }
                }

                SessionFactory.Flush(CollectionId.ToHash(), _dirty);

                _log.Log(string.Format("built in-memory index from {0} docs in {1}", docCount, timer.Elapsed));
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        private void Write(IDictionary document, Stream vectorStream)
        {
            var timer = new Stopwatch();
            timer.Start();

            var analyzed = new Dictionary<long, HashSet<string>>();

            Analyze(document, analyzed);

            Parallel.ForEach(analyzed, async column =>
            {
                var keyId = column.Key;
                var tokens = column.Value;
                var ix = GetIndex(keyId);
                var docId = ulong.Parse(document["__docid"].ToString());

                WriteIndex(docId, keyId, ix, tokens, vectorStream);

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
            });

            _log.Log(string.Format("indexed doc ID {0} in {1}", document["__docid"], timer.Elapsed));
        }

        private readonly object _sync = new object();

        private VectorNode GetIndex(long keyId)
        {
            VectorNode root;

            if (!_dirty.TryGetValue(keyId, out root))
            {
                if (!Index.TryGetValue(keyId, out root))
                {
                    lock (_sync)
                    {
                        if (!Index.TryGetValue(keyId, out root))
                        {
                            root = new VectorNode();
                            Index.GetOrAdd(keyId, root);
                        }
                    }
                }

                _dirty[keyId] = root;
            }

            return root;
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

        private void WriteIndex(ulong docId, long keyId, VectorNode index, IEnumerable<string> tokens, Stream vectorStream)
        {
            foreach (var token in tokens)
            {
                index.Add(new VectorNode(token, docId), vectorStream);
            }
        }
    }
}