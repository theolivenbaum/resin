using System.Collections.Generic;

namespace Sir.Store
{
    public class VectorTree
    {
        public int Count { get; private set; }
        public int MergeCount { get; private set; }

        private SortedList<ulong, SortedList<uint, VectorNode>> _ix;

        public VectorTree() : this(new SortedList<ulong, SortedList<uint, VectorNode>>()) { }

        public VectorTree(SortedList<ulong, SortedList<uint, VectorNode>> ix)
        {
            _ix = ix;
        }

        public SortedList<uint, VectorNode> GetOrCreateIndex(ulong collectionId)
        {
            SortedList<uint, VectorNode> ix;
            if(!_ix.TryGetValue(collectionId, out ix))
            {
                ix = new SortedList<uint, VectorNode>();
                _ix.Add(collectionId, ix);
            }
            return ix;
        }

        public (int depth, int width) Size(ulong collectionId, uint keyId)
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

        public VectorNode Find(ulong colId, uint keyId, string pattern)
        {
            return GetNode(colId, keyId).ClosestMatch(pattern);
        }

        public VectorNode GetNode(ulong colId, uint keyId)
        {
            SortedList<uint,VectorNode> nodes;
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

        public string Visualize(ulong collectionId, uint keyId)
        {
            return _ix[collectionId][keyId].Visualize();
        }

    }
}