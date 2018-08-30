using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// A tree of indexes (one per collection and key).
    /// </summary>
    public class VectorTree
    {
        public int Count { get; private set; }

        private SortedList<ulong, SortedList<long, VectorNode>> _ix;
        private object _sync = new object();

        public VectorTree() : this(new SortedList<ulong, SortedList<long, VectorNode>>()) { }

        public VectorTree(SortedList<ulong, SortedList<long, VectorNode>> ix)
        {
            _ix = ix;
        }

        public void Add(ulong collectionId, long keyId, VectorNode index)
        {
            SortedList<long, VectorNode> ix;

            if (!_ix.TryGetValue(collectionId, out ix))
            {
                lock (_sync)
                {
                    if (!_ix.TryGetValue(collectionId, out ix))
                    {
                        ix = new SortedList<long, VectorNode>();
                        _ix.Add(collectionId, ix);
                    }
                }
            }

            if (!ix.ContainsKey(keyId))
            {
                lock (_sync)
                {
                    if (!ix.ContainsKey(keyId))
                    {
                        ix.Add(keyId, index);
                    }
                }
            }
        }

        public SortedList<long, VectorNode> GetIndex(ulong collectionId)
        {
            SortedList<long, VectorNode> ix;

            if (!_ix.TryGetValue(collectionId, out ix))
            {
                return null;
            }

            return ix;
        }
    }
}