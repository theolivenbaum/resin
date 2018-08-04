using System.Collections.Generic;

namespace Sir.Store
{
    /// <summary>
    /// A tree of indexes (one per collection and key).
    /// </summary>
    public class VectorTree
    {
        public int Count { get; private set; }
        public int MergeCount { get; private set; }

        private SortedList<ulong, SortedList<long, VectorNode>> _ix;

        public VectorTree() : this(new SortedList<ulong, SortedList<long, VectorNode>>()) { }

        public VectorTree(SortedList<ulong, SortedList<long, VectorNode>> ix)
        {
            _ix = ix;
        }

        public SortedList<long, VectorNode> GetOrCreateIndex(ulong collectionId)
        {
            SortedList<long, VectorNode> ix;
            if(!_ix.TryGetValue(collectionId, out ix))
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

        public VectorNode Find(ulong colId, long keyId, string pattern)
        {
            return GetNode(colId, keyId).ClosestMatch(pattern);
        }

        public VectorNode GetNode(ulong colId, long keyId)
        {
            SortedList<long,VectorNode> nodes;
            if (!_ix.TryGetValue(colId, out nodes))
            {
                return null;
            }
            VectorNode node;
            if(!nodes.TryGetValue(keyId, out node))
            {
                return null;
            }
            return node;
        }

        public string Visualize(ulong collectionId, long keyId)
        {
            return _ix[collectionId][keyId].Visualize();
        }

    }
}