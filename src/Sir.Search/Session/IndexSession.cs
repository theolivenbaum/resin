using Sir.VectorSpace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Sir.Search
{
    /// <summary>
    /// Indexing session targeting a single collection.
    /// </summary>
    public class IndexSession<T> : IIndexSession, IDisposable
    {
        private readonly IModel<T> _model;
        private readonly IIndexingStrategy _indexingStrategy;
        private readonly ConcurrentDictionary<long, VectorNode> _index;

        /// <summary>
        /// Creates an instance of an indexing session targeting a single collection.
        /// </summary>
        /// <param name="sessionFactory">A session factory</param>
        /// <param name="model">A model</param>
        /// <param name="config">A configuration provider</param>
        /// <param name="logger">A logger</param>
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
                _indexingStrategy.ExecutePut(column, keyId, new VectorNode(vector, docId), _model);
            }
        }

        public VectorNode GetInMemoryIndex(long keyId)
        {
            return _index[keyId];
        }

        public IDictionary<long, VectorNode> GetInMemoryIndex()
        {
            return _index;
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