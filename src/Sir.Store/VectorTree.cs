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

        //public void SerializeTree(string dir)
        //{
        //    foreach (var index in _ix)
        //    {
        //        foreach (var key in index.Value)
        //        {
        //            var ixFileName = Path.Combine(dir, string.Format("{0}.{1}.ix", index.Key, key.Key));

        //            using (var ixStream = new FileStream(ixFileName, FileMode.Create, FileAccess.Write, FileShare.None))
        //            {
        //                key.Value.SerializeTree(ixStream);
        //            }
        //        }
        //    }
        //}

        public void Add(ulong collectionId, long keyId, VectorNode index)
        {
            SortedList<long, VectorNode> collection;

            if (!_ix.TryGetValue(collectionId, out collection))
            {
                lock (_sync)
                {
                    if (!_ix.TryGetValue(collectionId, out collection))
                    {
                        collection = new SortedList<long, VectorNode>();
                        collection.Add(keyId, index);

                        _ix.Add(collectionId, collection);
                    }
                }
            }
            else
            {
                if (!collection.ContainsKey(keyId))
                {
                    lock (_sync)
                    {
                        if (!collection.ContainsKey(keyId))
                        {
                            collection.Add(keyId, index);
                        }
                    }
                }
                else
                {
                    collection[keyId] = index;
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