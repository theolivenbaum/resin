using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Sir.VectorSpace
{
    /// <summary>
    /// Binary tree that consists of nodes that carry vectors as their payload. 
    /// Nodes are balanced according to the cosine similarity of their vectors.
    /// </summary>
    public class VectorNode
    {
        public const int BlockSize = sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long);

        private VectorNode _right;
        private VectorNode _left;
        private long _weight;

        public HashSet<long> DocIds { get; set; }
        public VectorNode Ancestor { get; private set; }
        public long ComponentCount { get; set; }
        public long VectorOffset { get; set; }
        public long PostingsOffset { get; set; }

        /// <summary>
        /// The vector of the node. NULL if node is root.
        /// </summary>
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
                var old = _right;

                _right = value;

                if (value != null)
                {
                    _right.Ancestor = this;
                    IncrementWeight();
                }
                else
                {
                    _weight = _weight - old.Weight - 1;
                }
            }
        }

        public VectorNode Left
        {
            get => _left;
            set
            {
                var old = _left;

                _left = value;

                if (value != null)
                {
                    _left.Ancestor = this;
                    IncrementWeight();
                }
                else
                {
                    _weight = _weight - old.Weight - 1;
                }
            }
        }

        public long Terminator { get; set; }

        public IList<long> PostingsOffsets { get; set; }

        public int Depth {
            get
            {
                var ancestor = Ancestor;
                var depth = 0;

                while (ancestor!= null)
                {
                    depth++;
                    ancestor = ancestor.Ancestor;
                }

                return depth;
            }
        }

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

        public VectorNode(IVector vector, long docId = -1, long postingsOffset = -1)
        {
            Vector = vector;
            ComponentCount = vector.ComponentCount;
            PostingsOffset = postingsOffset;
            VectorOffset = -1;

            if (docId > -1)
            {
                DocIds = new HashSet<long> { docId };
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
            return Vector == null ? "ROOT" : Vector.Label == null ? Vector.ToString() : Vector.Label.ToString();
        }

        public VectorNodeData ToData()
        {
            long terminator;

            if (Left == null && Right == null) // there are no children
            {
                terminator = 3;
            }
            else if (Left == null) // there is a right but no left
            {
                terminator = 2;
            }
            else if (Right == null) // there is a left but no right
            {
                terminator = 1;
            }
            else // there is a left and a right
            {
                terminator = 0;
            }

            return new VectorNodeData(VectorOffset, PostingsOffset, ComponentCount, Weight, terminator);
        }
    }

    public struct VectorNodeData
    {
        public long VectorOffset { get; }
        public long PostingsOffset { get; }
        public long ComponentCount { get; }
        public long Weight { get; }
        public long Terminator { get; }

        public VectorNodeData(long vectorOffset, long postingsOffset, long componentCount, long weight, long terminator)
        {
            VectorOffset = vectorOffset;
            PostingsOffset = postingsOffset;
            ComponentCount = componentCount;
            Weight = weight;
            Terminator = terminator;
        }
    }
}
