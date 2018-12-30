using System;
using System.Collections;
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
        private readonly StreamWriter _log;
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
            _log = Logging.CreateWriter("indexingsession");
            _validate = config.Get("create_index_validation_files") == "true";
            _dirty = new Dictionary<long, VectorNode>();
            _vectorStream = SessionFactory.CreateAsyncAppendStream(
                    Path.Combine(SessionFactory.Dir, string.Format("{0}.vec", CollectionId.ToHash())));
        }

        public async Task Write(IDictionary doc)
        {
            try
            {
                await IndexDocument(doc, _vectorStream);
            }
            catch (Exception ex)
            {
                _log.Log(ex);

                throw;
            }
        }

        public async Task Flush()
        {
            if (_flushed)
                return;

            await SerializeIndex(CollectionId.ToHash(), _dirty);

            _flushed = true;
        }

        private async Task SerializeIndex(ulong collectionId, IDictionary<long, VectorNode> columns)
        {
            var timer = new Stopwatch();
            timer.Start();

            var postingsWriter = new RemotePostingsWriter(_config);
            var didPublish = false;

            foreach (var x in columns)
            {
                await postingsWriter.Write(collectionId, x.Value);

                var pixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ixp", collectionId, x.Key));

                using (var pageIndexWriter = new PageIndexWriter(SessionFactory.CreateAsyncAppendStream(pixFileName)))
                using (var ixStream = CreateIndexStream(collectionId, x.Key))
                {
                    var page = await x.Value.SerializeTree(ixStream);

                    await pageIndexWriter.WriteAsync(page.offset, page.length);
                }

                didPublish = true;
            }

            if (didPublish)
                _log.Log(string.Format("***FLUSHED*** index in {0}", timer.Elapsed));
        }

        private Stream CreateIndexStream(ulong collectionId, long keyId)
        {
            var fileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", collectionId, keyId));
            return SessionFactory.CreateAsyncAppendStream(fileName);
        }

        private async Task IndexDocument(IDictionary document, Stream vectorStream)
        {
            var timer = new Stopwatch();
            timer.Start();

            var analyzed = new Dictionary<long, HashSet<string>>();

            Analyze(document, analyzed);

            foreach (var column in analyzed)
            {
                var keyId = column.Key;
                var tokens = column.Value;
                var ix = GetIndex(keyId);
                var docId = ulong.Parse(document["__docid"].ToString());

                await WriteTokens(docId, keyId, ix, tokens, vectorStream);

                // validate
                if (_validate)
                {
                    foreach (var token in tokens)
                    {
                        var query = new VectorNode(token);
                        var closestMatch = ix.ClosestMatch(query);

                        if (closestMatch.Score < VectorNode.IdenticalAngle)
                        {
                            throw new DataMisalignedException();
                        }
                    }

                    File.WriteAllText(
                        Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.{2}.validate", CollectionId.ToHash(), keyId, document["__docid"])),
                        string.Join('\n', tokens));
                }
            }

            _log.Log(string.Format("indexed doc ID {0} in {1}", document["__docid"], timer.Elapsed));
        }

        private VectorNode GetIndex(long keyId)
        {
            VectorNode root;

            if (!_dirty.TryGetValue(keyId, out root))
            {
                root = new VectorNode();
                _dirty.Add(keyId, root);
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

        private async Task WriteTokens(ulong docId, long keyId, VectorNode index, IEnumerable<string> tokens, Stream vectorStream)
        {
            foreach (var token in tokens)
            {
                await index.Add(new VectorNode(token, docId), vectorStream);
            }
        }

        public void Dispose()
        {
            if (!_flushed)
            {
                Task.WaitAll(new[] { Flush() });
            }

            _vectorStream.Dispose();
        }
    }
}