using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class TermIndexSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly IStringModel _model;
        private readonly ConcurrentDictionary<long, List<VectorNode>> _secondaryIndex;
        private readonly ConcurrentDictionary<long, VectorNode> _primaryIndex;
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
            _secondaryIndex = new ConcurrentDictionary<long, List<VectorNode>>();
            _primaryIndex = new ConcurrentDictionary<long, VectorNode>();
            _postingsStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos"));
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.vec"));
        }

        public void Put(long docId, long keyId, string value)
        {
            var terms = _model.Tokenize(value).Embeddings;
            var primix = GetOrCreatePrimaryIndex(keyId);

            foreach (var term in terms)
            {
                VectorNode x;
                long indexId;

                if (!GraphBuilder.MergeOrAddPrimary(primix, new VectorNode(term, docId), _model, out x))
                {
                    x.PostingsOffset = indexId = primix.Weight - 1;
                }
                else
                {
                    indexId = x.PostingsOffset;
                }

                PutSecondary((int)indexId, docId, keyId, term);
            }
        }

        private void PutSecondary(int indexId, long docId, long keyId, IVector term)
        {
            var ix = GetOrCreateSecondaryIndex(keyId, (int)indexId);

            var node = new VectorNode(term, docId);
            VectorNode x;

            if (GraphBuilder.MergeOrAdd(ix, node, _model, out x))
            {
                GraphBuilder.AddDocId(x, docId);
            }
        }

        private VectorNode GetOrCreateSecondaryIndex(long keyId, int indexId)
        {
            var list = _secondaryIndex.GetOrAdd(keyId, new List<VectorNode>());

            if (indexId > list.Count - 1)
            {
                list.Add(new VectorNode());
            }

            return list[indexId];
        }

        private VectorNode GetOrCreatePrimaryIndex(long keyId)
        {
            return _primaryIndex.GetOrAdd(keyId, new VectorNode());
        }

        public IEnumerable<GraphStats> GetStats()
        {
            foreach (var node in _primaryIndex)
            {
                yield return new GraphStats(node.Key, node.Value);
            }

            //foreach (var list in _secondaryIndex)
            //{
            //    foreach (var node in list.Value)
            //        yield return new GraphStats(list.Key, node);
            //}
        }

        private void Serialize()
        {
            foreach (var column in _primaryIndex)
            {
                using (var writer = new ColumnWriter(CollectionId, column.Key, SessionFactory))
                    writer.CreatePage(column.Value, _vectorStream, _postingsStream, _model);
            }

            _primaryIndex.Clear();

            foreach (var column in _secondaryIndex)
            {
                using (var writer = new ColumnWriter(CollectionId, column.Key, SessionFactory, "ixs"))
                {
                    foreach (var node in column.Value)
                        writer.CreatePage(node, _vectorStream, _postingsStream, _model);
                }
            }

            _secondaryIndex.Clear();
        }

        public void Dispose()
        {
            Serialize();

            _postingsStream.Dispose();
            _vectorStream.Dispose();

            SessionFactory.ClearPageInfo();
        }

        //private void Validate((long keyId, long docId, AnalyzedData tokens) item)
        //{
        //    var tree = GetOrCreateSecondaryIndex(item.keyId);

        //    foreach (var vector in item.tokens.Embeddings)
        //    {
        //        var hit = PathFinder.ClosestMatch(tree, vector, _model);

        //        if (hit.Score < _model.IdenticalAngle)
        //        {
        //            throw new DataMisalignedException();
        //        }

        //        var valid = false;

        //        foreach (var id in hit.Node.DocIds)
        //        {
        //            if (id == item.docId)
        //            {
        //                valid = true;
        //                break;
        //            }
        //        }

        //        if (!valid)
        //        {
        //            throw new DataMisalignedException();
        //        }
        //    }
        //}
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