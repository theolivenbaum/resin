using Sir.VectorSpace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Sir.Search
{
    public class IndexSession<T> : IIndexSession, IDisposable
    {
        private readonly IModel<T> _model;
        private readonly IIndexingStrategy _indexingStrategy;
        private readonly ConcurrentDictionary<long, VectorNode> _index;

        public IndexSession(
            IModel<T> model,
            IIndexingStrategy indexingStrategy)
        {
            _model = model;
            _indexingStrategy = indexingStrategy;
            _index = new ConcurrentDictionary<long, VectorNode>();
        }

        public void Put(long docId, long keyId, T value)
        {
            var vectors = _model.Tokenize(value);
            var column = _index.GetOrAdd(keyId, new VectorNode());

            foreach (var vector in vectors)
            {
                _indexingStrategy.ExecutePut<T>(column, keyId, new VectorNode(vector, docId));
            }
        }

        //public void Put(long docId, long keyId, T value)
        //{
        //    var vectors = _model.Tokenize(value);
        //    var tree = new VectorNode();

        //    foreach (var vector in vectors)
        //    {
        //        _indexingStrategy.ExecutePut<T>(tree, keyId, new VectorNode(vector, docId));
        //    }

        //    var indexQueue = _indexQueue.GetOrAdd(keyId, new ProducerConsumerQueue<(long, long, VectorNode)>(TreeConsumer));

        //    indexQueue.Enqueue((docId, keyId, tree));
        //}

        public IndexInfo GetIndexInfo()
        {
            return new IndexInfo(GetGraphInfo());
        }

        public IDictionary<long, VectorNode> GetInMemoryIndex()
        {
            return _index;
        }

        private IEnumerable<GraphInfo> GetGraphInfo()
        {
            foreach (var ix in _index)
            {
                yield return new GraphInfo(ix.Key, ix.Value);
            }
        }

        public void Dispose()
        {
        }
    }
}