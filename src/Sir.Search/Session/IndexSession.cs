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

        public IDictionary<long, VectorNode> InMemoryIndex => _index;

        public IndexSession(
            IModel<T> model,
            IIndexingStrategy indexingStrategy)
        {
            _model = model;
            _index = new ConcurrentDictionary<long, VectorNode>();
            _indexingStrategy = indexingStrategy;
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

        public IndexInfo GetIndexInfo()
        {
            return new IndexInfo(GetGraphInfo());
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