using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Sir.VectorSpace
{
    /// <summary>
    /// Binary tree that consists of nodes that carry vectors as their payload. 
    /// Nodes are balanced by taking into account the cosine similarity of their neighbouring vectors.
    /// </summary>
    public class VectorNode
    {
        public const int BlockSize = sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long);

        private VectorNode _right;
        private VectorNode _left;
        private long _weight;

        public List<long> DocIds { get; set; }
        public VectorNode Ancestor { get; private set; }
        public long ComponentCount { get; set; }
        public long VectorOffset { get; set; }
        public long PostingsOffset { get; set; }
        public IVector Vector { get; set; }

        public object Sync { get; } = new object();

        public long Weight
        {
            get { return _weight; }
        }

        public VectorNode Right
        {
            get => _right;
            set
            {
                _right = value;
                _right.Ancestor = this;
                IncrementWeight();
            }
        }

        public VectorNode Left
        {
            get => _left;
            set
            {
                _left = value;
                _left.Ancestor = this;
                IncrementWeight();
            }
        }

        public long Terminator { get; set; }

        public IList<long> PostingsOffsets { get; set; }

        public bool IsRoot => Ancestor == null && Vector == null;

        public long? KeyId { get; set; }

        public VectorNode()
        {
            PostingsOffset = -1;
            VectorOffset = -1;
        }

        public VectorNode(long postingsOffset)
        {
            PostingsOffset = -1;
            PostingsOffsets = new List<long> { postingsOffset }; ;
            VectorOffset = -1;
        }

        public VectorNode(IVector vector = null, long docId = -1, long postingsOffset = -1, long? keyId = null, List<long> docIds = null)
        {
            Vector = vector;
            ComponentCount = vector == null ? 0 : vector.ComponentCount;
            PostingsOffset = postingsOffset;
            VectorOffset = -1;
            DocIds = docIds;
            KeyId = keyId;

            if (docId > -1)
            {
                if (DocIds == null)
                {
                    DocIds = new List<long> { docId };
                }
                else
                {
                    DocIds.Add(docId);
                }
            }

            if (postingsOffset > -1)
            {
                PostingsOffsets = new List<long> { postingsOffset };
            }
        }

        public VectorNode(long postingsOffset, long vecOffset, long terminator, long weight, IVector vector)
        {
            PostingsOffset = postingsOffset;
            VectorOffset = vecOffset;
            Terminator = terminator;
            _weight = weight;
            ComponentCount = vector.ComponentCount;
            Vector = vector;
        }

        public void MergeOrAddConcurrent(
            VectorNode node,
            IModel model)
        {
            var cursor = this;

            while (true)
            {
                var angle = cursor.Vector == null ? 0 : model.CosAngle(node.Vector, cursor.Vector);

                if (angle >= model.IdenticalAngle)
                {
                    cursor.MergeDocIdsConcurrent(node);

                    break;
                }
                else if (angle > model.FoldAngle)
                {
                    if (cursor.Left == null)
                    {
                        if (Interlocked.CompareExchange(ref cursor._left, node, null) == null)
                        {
                            node.Ancestor = cursor;
                            cursor.IncrementWeight();
                            break;
                        }
                        else
                        {
                            cursor.MergeOrAddConcurrent(node, model);
                        }
                    }
                    else
                    {
                        cursor = cursor.Left;
                    }
                }
                else
                {
                    if (cursor.Right == null)
                    {
                        if (Interlocked.CompareExchange(ref cursor._right, node, null) == null)
                        {
                            node.Ancestor = cursor;
                            cursor.IncrementWeight();
                            break;
                        }
                        else
                        {
                            cursor.MergeOrAddConcurrent(node, model);
                        }
                    }
                    else
                    {
                        cursor = cursor.Right;
                    }
                }
            }
        }

        public void IncrementWeight()
        {
            Interlocked.Increment(ref _weight);

            var cursor = Ancestor;

            while (cursor != null)
            {
                Interlocked.Increment(ref cursor._weight);

                cursor = cursor.Ancestor;
            }
        }

        public override string ToString()
        {
            return IsRoot ? "*" : Vector.Label == null ? Vector.ToString() : Vector.Label.ToString();
        }
    }
}
