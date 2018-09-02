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

        public void Serialize(string dir)
        {
            foreach (var index in _ix)
            {
                var vecFn = Path.Combine(dir, string.Format("{0}.vec", index.Key));
                var posFn = Path.Combine(dir, string.Format("{0}.pos", index.Key));

                using (var vectorStream = new FileStream(vecFn, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var postingsStream = new FileStream(posFn, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    foreach (var key in index.Value)
                    {
                        var ixFileName = Path.Combine(dir, string.Format("{0}.{1}.ix", index.Key, key.Key));

                        using (var ixStream = new FileStream(ixFileName, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            key.Value.Serialize(ixStream, vectorStream, postingsStream);
                        }
                    }
                }
            }
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