using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class TermIndexSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly IStringModel _model;
        private readonly ConcurrentDictionary<long, VectorNode> _dirty;
        private Stream _postingsStream;
        private readonly Stream _vectorStream;

        public TermIndexSession(
            ulong collectionId,
            SessionFactory sessionFactory, 
            IStringModel model,
            IConfigurationProvider config) : base(collectionId, sessionFactory)
        {
            _config = config;
            _model = model;
            _dirty = new ConcurrentDictionary<long, VectorNode>();
            _postingsStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos"));
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.vec"));
        }

        public void Put(long docId, long keyId, string value)
        {
            var ix = GetOrCreateIndex(keyId);
            var tokens = _model.Tokenize(value);

            foreach (var vector in tokens.Embeddings)
            {
                var node = new VectorNode(vector, docId);
                VectorNode x;

                if (GraphBuilder.GetOrAdd(ix, node, _model, out x))
                {
                    GraphBuilder.AddDocId(x, docId);
                }
            }

            if (ix.Weight >= _model.PageWeight)
            {
                CreatePage(keyId);
            }
        }

        private VectorNode GetOrCreateIndex(long keyId)
        {
            return _dirty.GetOrAdd(keyId, new VectorNode());
        }

        public IEnumerable<GraphStats> GetStats()
        {
            foreach(var node in _dirty)
            {
                yield return new GraphStats(node.Key, node.Value);
            }
        }

        public void CreatePage(long keyId)
        {
            using (var serializer = new ColumnWriter(CollectionId, keyId, SessionFactory))
            {
                serializer.CreatePage(_dirty[keyId], _vectorStream, _postingsStream, _model);
            }

            _dirty.Remove(keyId, out _);

            SessionFactory.ClearPageInfo();
        }

        public void Dispose()
        {
            foreach (var column in _dirty.Keys)
            {
                CreatePage(column);
            }

            _postingsStream.Dispose();
            _vectorStream.Dispose();
        }

        private void Validate((long keyId, long docId, AnalyzedData tokens) item)
        {
            var tree = GetOrCreateIndex(item.keyId);

            foreach (var vector in item.tokens.Embeddings)
            {
                var hit = PathFinder.ClosestMatch(tree, vector, _model);

                if (hit.Score < _model.IdenticalAngle)
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

    public class GraphStats
    {
        private readonly long _keyId;
        private readonly VectorNode _graph;

        public GraphStats(long keyId, VectorNode graph)
        {
            _keyId = keyId;
            _graph = graph;
        }

        public override string ToString()
        {
            return $"key: {_keyId} weight: {_graph.Weight} depth/width: {PathFinder.Size(_graph)}";
        }
    }
}