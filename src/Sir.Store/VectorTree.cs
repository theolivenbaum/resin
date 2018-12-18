using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Sir.Store
{
    /// <summary>
    /// A tree of indexes (one per collection and key).
    /// </summary>
    public class VectorTree
    {
        public int Count { get; private set; }

        private ConcurrentDictionary<ulong, ConcurrentDictionary<long, IList<VectorNode>>> _ix;

        public VectorTree() : this(new ConcurrentDictionary<ulong, ConcurrentDictionary<long, IList<VectorNode>>>()) { }

        public VectorTree(ConcurrentDictionary<ulong, ConcurrentDictionary<long, IList<VectorNode>>> ix)
        {
            _ix = ix;
        }

        public IEnumerable<(ulong collectionId, long keyId, IList<VectorNode> index)> All()
        {
            foreach (var collection in _ix)
            {
                foreach (var column in collection.Value)
                {
                    yield return (collection.Key, column.Key, column.Value);
                }
            }
        }

        //public void Add(ulong collectionId, ConcurrentDictionary<long, VectorNode> index)
        //{
        //    ConcurrentDictionary<long, IList<VectorNode>> collection;

        //    if (_ix.TryGetValue(collectionId, out collection))
        //    {
        //        throw new InvalidOperationException();
        //    }
        //    else
        //    {
        //        _ix.GetOrAdd(collectionId, index);
        //    }
        //}

        //public void Add(ulong collectionId, long keyId, VectorNode index)
        //{
        //    ConcurrentDictionary<long, VectorNode> collection;

        //    if (!_ix.TryGetValue(collectionId, out collection))
        //    {
        //        collection = new ConcurrentDictionary<long, VectorNode>();
        //        collection.GetOrAdd(keyId, index);

        //        _ix.GetOrAdd(collectionId, collection);
        //    }
        //    else
        //    {
        //        if (!collection.ContainsKey(keyId))
        //        {
        //            collection.GetOrAdd(keyId, index);
        //        }
        //        else
        //        {
        //            collection[keyId] = index;
        //        }
        //    }
        //}

        public ConcurrentDictionary<long, IList<VectorNode>> GetIndex(ulong collectionId)
        {
            ConcurrentDictionary<long, IList<VectorNode>> ix;

            if (!_ix.TryGetValue(collectionId, out ix))
            {
                return null;
            }

            return ix;
        }
    }
}