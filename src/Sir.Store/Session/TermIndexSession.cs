using Sir.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly IDictionary<long, List<VectorNode>> _secondaryIndex;
        private readonly ConcurrentDictionary<long, VectorNode> _primaryIndex;
        private readonly ProducerConsumerQueue<(int indexId, long docId, long keyId, IVector term)> _workers;
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
            _secondaryIndex = new Dictionary<long, List<VectorNode>>();
            _primaryIndex = new ConcurrentDictionary<long, VectorNode>();
            _postingsStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos"));
            _vectorStream = SessionFactory.CreateAppendStream(Path.Combine(SessionFactory.Dir, $"{CollectionId}.vec"));
            _workers = new ProducerConsumerQueue<(int, long, long, IVector)>(1, PutSecondary);
        }

        public void Put(long docId, long keyId, string value)
        {
            var terms = _model.Tokenize(value).Embeddings;
            var primix = GetOrCreatePrimaryIndex(keyId);

            foreach (var term in terms)
            {
                VectorNode x;
                long indexId;
                int i;

                if (!GraphBuilder.MergeOrAdd(primix, new VectorNode(term, docId), _model.PrimaryIndexIdenticalAngle, _model.PrimaryIndexFoldAngle, _model, out x))
                {
                    x.PostingsOffset = indexId = primix.Weight - 1;
                    i = (int)indexId;
                    GetOrCreateSecondaryIndex(keyId, i);
                }
                else
                {
                    indexId = x.PostingsOffset;
                    i = (int)indexId;
                }

                _workers.Enqueue((i, docId, keyId, term));
            }
        }

        private void PutSecondary((int indexId, long docId, long keyId, IVector term) workItem)
        {
            var ix = GetOrCreateSecondaryIndex(workItem.keyId, workItem.indexId);

            var node = new VectorNode(workItem.term, workItem.docId);
            VectorNode x;

            if (GraphBuilder.MergeOrAdd(ix, node, _model.IdenticalAngle, _model.FoldAngle, _model, out x))
            {
                GraphBuilder.AddDocId(x, workItem.docId);
            }
        }

        private VectorNode GetOrCreateSecondaryIndex(long keyId, int indexId)
        {
            List<VectorNode> list;

            if (!_secondaryIndex.TryGetValue(keyId, out list))
            {
                list = new List<VectorNode>();
                _secondaryIndex.Add(keyId, list);
            }

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

        public IndexInfo GetIndexInfo()
        {
            return new IndexInfo(_workers.Count, GetGraphInfo());
        }

        private IEnumerable<GraphInfo> GetGraphInfo()
        {
            foreach (var node in _primaryIndex)
            {
                yield return new GraphInfo("primary", node.Key, node.Value);
            }

            foreach (var list in _secondaryIndex)
            {
                var i = 0;

                foreach (var node in list.Value)
                    yield return new GraphInfo($"secondary{i++}", list.Key, node);
            }
        }

        private void Serialize()
        {
            foreach (var column in _primaryIndex)
            {
                using (var writer = new ColumnWriter(CollectionId, column.Key, SessionFactory))
                {
                    writer.CreatePage(column.Value, _vectorStream, _postingsStream, _model);
                }
            }

            _primaryIndex.Clear();

            foreach (var column in _secondaryIndex)
            {
                using (var writer = new ColumnWriter(CollectionId, column.Key, SessionFactory, "ixs"))
                {
                    foreach (var node in column.Value)
                    {
                        writer.CreatePage(node, _vectorStream, _postingsStream, _model);
                    }
                }
            }

            _secondaryIndex.Clear();
        }

        public void Dispose()
        {
            var time = Stopwatch.StartNew();

            this.Log("synchronizing");

            _workers.Dispose();

            this.Log($"synchronized for {time.Elapsed}");

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

    public class IndexInfo
    {
        public int QueueLength { get; }
        public IEnumerable<GraphInfo> Info { get; }

        public IndexInfo(int queueLength, IEnumerable<GraphInfo> stats)
        {
            QueueLength = queueLength;
            Info = stats;
        }
    }

    public class GraphInfo
    {
        private readonly string _name;
        private readonly long _keyId;
        private readonly VectorNode _graph;

        public long Weight => _graph.Weight;

        public GraphInfo(string name, long keyId, VectorNode graph)
        {
            _name = name;
            _keyId = keyId;
            _graph = graph;
        }

        public override string ToString()
        {
            return $"{_name} key: {_keyId} weight: {_graph.Weight} depth/width: {PathFinder.Size(_graph)}";
        }
    }
}