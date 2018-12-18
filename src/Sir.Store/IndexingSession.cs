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
        private readonly IDictionary<long, VectorNode> _dirty;
        private bool _flushed;

        public IndexingSession(
            string collectionId, 
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationService config) : base(collectionId, sessionFactory)
        {
            _tokenizer = tokenizer;
            _log = Logging.CreateWriter("indexingsession");
            _validate = config.Get("create_index_validation_files") == "true";
            _dirty = new Dictionary<long, VectorNode>();
        }

        public async Task Write(IndexingJob job)
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
                        await WriteDocument(doc, vectorStream);
                        docCount++;
                    }
                }

                _log.Log(string.Format("indexed {0} docs in {1}", docCount, timer.Elapsed));
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

            await SessionFactory.Flush(CollectionId.ToHash(), _dirty);

            _flushed = true;
        }

        private async Task WriteDocument(IDictionary document, Stream vectorStream)
        {
            var timer = new Stopwatch();
            timer.Start();

            var analyzed = new Dictionary<long, HashSet<string>>();

            Analyze(document, analyzed);

            foreach(var column in analyzed)
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
                        var closestMatch = ix.ClosestMatch(query, skipDirtyNodes: false);

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

        public override void Dispose()
        {
            base.Dispose();

            if (!_flushed)
            {
                Task.WaitAll(new[] { Flush() });
            }
        }
    }
}