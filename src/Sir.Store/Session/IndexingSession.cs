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
    public class IndexingSession : CollectionSession, IDisposable
    {
        private readonly IConfigurationProvider _config;
        private readonly ITokenizer _tokenizer;
        private bool _validate;
        private readonly IDictionary<long, VectorNode> _dirty;
        private readonly Stream _vectorStream;
        private bool _flushed;

        public IndexingSession(
            string collectionId, 
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config) : base(collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _validate = config.Get("create_index_validation_files") == "true";
            _dirty = new ConcurrentDictionary<long, VectorNode>();
            _vectorStream = SessionFactory.CreateAppendStream(
                Path.Combine(SessionFactory.Dir, string.Format("{0}.vec", CollectionId.ToHash())));
        }

        public void Write(IDictionary document)
        {
            try
            {
                var timer = Stopwatch.StartNew();
                var docId = ulong.Parse(document["__docid"].ToString());

                foreach (var column in Analyze(document))
                {
                    var keyId = column.Key;
                    var tokens = column.Value;

                    WriteTokens(docId, keyId, tokens);

                    // validate
                    if (_validate)
                    {
                        var ix = _dirty[keyId];

                        foreach (var token in tokens.Tokens)
                        {
                            var termVector = tokens.ToCharVector(token.offset, token.length);
                            var query = new VectorNode(termVector);
                            var closestMatch = ix.ClosestMatch(query);

                            if (closestMatch.Score < VectorNode.IdenticalAngle)
                            {
                                throw new DataMisalignedException();
                            }
                        }

                        File.WriteAllText(
                            Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.{2}.validate", CollectionId.ToHash(), keyId, docId)),
                            string.Join('\n', tokens));
                    }
                }

                Logging.Log(string.Format("added doc ID {0} to in-mem index in {1}", document["__docid"], timer.Elapsed));

            }
            catch (Exception ex)
            {
                Logging.Log(ex);

                throw;
            }
        }

        public void Flush()
        {
            if (_flushed)
                return;

            var tasks = new Task[_dirty.Count];
            var taskId = 0;

            foreach (var column in _dirty)
            {
                tasks[taskId++] = SerializeColumn(column.Key, column.Value);
            }

            _vectorStream.Flush();
            _vectorStream.Close();
            _vectorStream.Dispose();

            Task.WaitAll(tasks);

            _flushed = true;

            Logging.Log(string.Format("***FLUSHED***"));
        }

        private async Task SerializeColumn(long keyId, VectorNode column)
        {
            var time = Stopwatch.StartNew();
            (int depth, int width, int avgDepth) size;

            using (var postingsWriter = new RemotePostingsWriter(_config))
            {
                var collectionId = CollectionId.ToHash();

                await postingsWriter.Write(collectionId, column);

                var pixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ixp", collectionId, keyId));

                using (var pageIndexWriter = new PageIndexWriter(SessionFactory.CreateAppendStream(pixFileName)))
                using (var ixStream = CreateIndexStream(collectionId, keyId))
                {
                    var page = column.SerializeTree(ixStream);

                    pageIndexWriter.Write(page.offset, page.length);

                    size = column.Size();
                }
            }

            Logging.Log("serialized column {0} in {1} with size {2},{3} (avg depth {4})",
                keyId, time.Elapsed, size.depth, size.width, size.avgDepth);
        }

        private IDictionary<long, AnalyzedString> Analyze(IDictionary doc)
        {
            var result = new Dictionary<long, AnalyzedString>();
            var docId = (ulong)doc["__docid"];

            foreach (var obj in doc.Keys)
            {
                var key = (string)obj;

                if (!key.StartsWith("__"))
                {
                    var keyHash = key.ToHash();
                    var keyId = SessionFactory.GetKeyId(keyHash);
                    var val = (IComparable)doc[key];
                    var str = val as string;

                    if (str == null || key[0] == '_')
                    {
                        var v = val.ToString();

                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            result.Add(keyId, new AnalyzedString { Source = v.ToCharArray(), Tokens = new List<(int, int)> { (0, v.Length) } });
                        }
                    }
                    else
                    {
                        var tokens = _tokenizer.Tokenize(str);

                        result.Add(keyId, tokens);
                    }
                }
            }

            return result;
        }

        private void WriteTokens(ulong docId, long keyId, AnalyzedString tokens)
        {
            var ix = GetOrCreateIndex(keyId);

            foreach (var token in tokens.Tokens)
            {
                var termVector = tokens.ToCharVector(token.offset, token.length);

                ix.Add(new VectorNode(termVector, docId), _vectorStream);
            }
        }

        private Stream CreateIndexStream(ulong collectionId, long keyId)
        {
            var fileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", collectionId, keyId));
            return SessionFactory.CreateAppendStream(fileName);
        }

        private VectorNode GetOrCreateIndex(long keyId)
        {
            VectorNode root;

            if (!_dirty.TryGetValue(keyId, out root))
            {
                root = new VectorNode();
                _dirty.Add(keyId, root);
            }

            return root;
        }

        public void Dispose()
        {
            if (!_flushed)
            {
                throw new InvalidOperationException();
            }

            Logging.Close();
        }
    }
}