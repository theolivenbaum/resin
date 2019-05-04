using Sir.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class TermIndexSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly ITokenizer _tokenizer;
        private readonly ConcurrentDictionary<long, VectorNode> _dirty;
        private readonly ConcurrentDictionary<long, Stream> _vectorStreams;
        private readonly ConcurrentDictionary<long, long> _vectorStreamStartPositions;
        private bool _flushed;
        private bool _flushing;
        private readonly ProducerConsumerQueue<(long docId, long keyId, AnalyzedString tokens)> _modelBuilder;
        private readonly long[] _excludeKeyIds;

        public TermIndexSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config,
            params long[] excludeKeyIds) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _dirty = new ConcurrentDictionary<long, VectorNode>();
            _vectorStreams = new ConcurrentDictionary<long, Stream>();
            _vectorStreamStartPositions = new ConcurrentDictionary<long, long>();

            var numThreads = int.Parse(_config.Get("write_thread_count"));

            _modelBuilder = new ProducerConsumerQueue<(long docId, long keyId, AnalyzedString tokens)>(
                numThreads, BuildModel);

            _excludeKeyIds = excludeKeyIds;
        }

        /// <summary>
        /// Fields prefixed with "___" or "__" will not be indexed.
        /// Fields prefixed with "_" will not be tokenized.
        /// </summary>
        public void Index(IDictionary document)
        {
            var docId = (long)document["___docid"];

            foreach (var obj in document.Keys)
            {
                var key = (string)obj;
                AnalyzedString tokens = null;

                if (!key.StartsWith("__"))
                {
                    var keyHash = key.ToHash();
                    var keyId = SessionFactory.GetKeyId(CollectionId, keyHash);

                    if (_excludeKeyIds.Contains(keyId))
                    {
                        continue;
                    }

                    var val = document[key];
                    var str = val as string;

                    if (str == null || key[0] == '_')
                    {
                        var v = val.ToString();

                        if (!string.IsNullOrWhiteSpace(v))
                        {
                            tokens = new AnalyzedString
                            {
                                Original = v,
                                Source = v.ToCharArray(),
                                Tokens = new List<(int, int)> { (0, v.Length) }
                            };
                        }
                    }
                    else
                    {
                        tokens = _tokenizer.Tokenize(str);
                    }

                    if (tokens != null)
                    {
                        _modelBuilder.Enqueue((docId, keyId, tokens));
                    }
                }
            }

            this.Log("analyzed document {0} ", docId);
        }

        private void BuildModel((long docId, long keyId, AnalyzedString tokens) item)
        {
            var ix = GetOrCreateIndex(item.keyId);
            var vectorStream = GetOrCreateVectorStream(item.keyId);

            foreach (var vector in item.tokens.Embeddings())
            {
                ix.Add(new VectorNode(vector, item.docId), CosineSimilarity.Term, vectorStream);
            }
        }

        public void Flush()
        {
            if (_flushing || _flushed)
                return;

            _flushing = true;

            this.Log("waiting for model builder");

            using (_modelBuilder)
            {
                _modelBuilder.Join();
            }

            var columnTasks = new ConcurrentBag<Task>();
            var columnWriters = new ConcurrentBag<ColumnSerializer>();

            foreach (var column in _dirty)
            {
                var writer = new ColumnSerializer(
                    CollectionId, column.Key, SessionFactory, new RemotePostingsWriter(_config, CollectionName));

                columnWriters.Add(writer);

                columnTasks.Add(writer.CreateColumnSegment(column.Value));

                var vixpFileName = Path.Combine(
                    SessionFactory.Dir,
                    $"{CollectionId}.{column.Key}.vixp");

                using (var vectorStream = _vectorStreams[column.Key])
                using (var vixpStream = SessionFactory.CreateAppendStream(vixpFileName))
                using (var vixpWriter = new PageIndexWriter(vixpStream))
                {
                    vectorStream.Flush();

                    var offset = _vectorStreamStartPositions[column.Key];
                    var length = vectorStream.Length - offset;

                    vixpWriter.Write(offset, length);
                }
            }

            Task.WaitAll(columnTasks.ToArray());

            foreach(var writer in columnWriters)
            {
                writer.Dispose();
            }

            _flushed = true;
            _flushing = false;

            this.Log(string.Format("***FLUSHED***"));
        }

        private void Validate((long keyId, long docId, AnalyzedString tokens) item)
        {
            if (item.keyId == 4 || item.keyId == 5)
            {
                var tree = GetOrCreateIndex(item.keyId);

                foreach (var vector in item.tokens.Embeddings())
                {
                    var hit = tree.ClosestMatch(vector, CosineSimilarity.Term.foldAngle);

                    if (hit.Score < CosineSimilarity.Term.identicalAngle)
                    {
                        throw new DataMisalignedException();
                    }

                    var valid = false;

                    foreach (var id in hit.Node.DocIds)
                    {
                        if (id == item.docId)
                        {
                            valid = true;
                            break;
                        }
                    }

                    if (!valid)
                    {
                        throw new DataMisalignedException();
                    }
                }
            }
        }

        private VectorNode GetOrCreateIndex(long keyId)
        {
            return _dirty.GetOrAdd(keyId, new VectorNode());
        }

        private Stream GetOrCreateVectorStream(long keyId)
        {
            return _vectorStreams.GetOrAdd(keyId, key =>
                {
                    var stream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.{keyId}.vec"));

                    _vectorStreamStartPositions[keyId] = stream.Position;

                    return stream;
                }
            );
        }

        public void Dispose()
        {
            Flush();
        }
    }
}