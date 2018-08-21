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

        public VectorTree() : this(new SortedList<ulong, SortedList<long, VectorNode>>()) { }

        public VectorTree(SortedList<ulong, SortedList<long, VectorNode>> ix)
        {
            _ix = ix;
        }

        public void Add(ulong collectionId, long keyId, VectorNode index)
        {
            _ix[collectionId].Add(keyId, index);
        }

        public SortedList<long, VectorNode> GetOrCreateIndex(ulong collectionId)
        {
            SortedList<long, VectorNode> ix;

            if (!_ix.TryGetValue(collectionId, out ix))
            {
                ix = new SortedList<long, VectorNode>();
                _ix.Add(collectionId, ix);
            }

            return ix;
        }

        public (int depth, int width) Size(ulong collectionId, long keyId)
        {
            var root = _ix[collectionId][keyId];

            var width = 0;
            var depth = 0;
            var node = root.Right;

            while (node != null)
            {
                var d = node.Depth();
                if (d > depth)
                {
                    depth = d;
                }
                width++;
                node = node.Right;
            }

            return (depth, width);
        }
    }
}