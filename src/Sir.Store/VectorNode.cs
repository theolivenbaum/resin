using System.Collections.Generic;
using System.Text;

namespace Sir.Store
{
    /// <summary>
    /// Binary tree that consists of nodes that carry vectors as their payload. 
    /// Nodes are balanced according to the cosine similarity of their vectors.
    /// </summary>
    public class VectorNode
    {
        public const int BlockSize = sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long);
        public const int ComponentSize = sizeof(int) + sizeof(int);

        private VectorNode _right;
        private VectorNode _left;
        private VectorNode _ancestor;
        private long _weight;
        private float _angleWhenAdded;
        private object _sync = new object();

        public HashSet<long> DocIds { get; set; }
        public VectorNode Ancestor { get { return _ancestor; } }
        public long ComponentCount { get; set; }
        public long VectorOffset { get; set; }
        public long PostingsOffset { get; set; }
        public Vector Vector { get; set; }
        public object Sync { get { return _sync; } }

        public long Weight
        {
            get { return _weight; }
            set
            {
                var diff = value - _weight;

                _weight = value;

                if (diff > 0)
                {
                    var cursor = _ancestor;
                    while (cursor != null)
                    {
                        cursor._weight += diff;
                        cursor = cursor._ancestor;
                    }
                }
            }
        }

        public VectorNode Right
        {
            get => _right;
            set
            {
                _right = value;
                _right._ancestor = this;
                Weight++;
            }
        }

        public VectorNode Left
        {
            get => _left;
            set
            {
                _left = value;
                _left._ancestor = this;
                Weight++;
            }
        }

        public long Terminator { get; set; }

        public IList<long> PostingsOffsets { get; set; }
        public float AngleWhenAdded { get => _angleWhenAdded; set => _angleWhenAdded = value; }

        public VectorNode()
            : this(new Vector(new int[0]))
        {
        }

        public VectorNode(Vector vector)
        {
            Vector = vector;
            PostingsOffset = -1;
            VectorOffset = -1;
        }

        public VectorNode(Vector vector, IList<long> postingsOffsets)
        {
            Vector = vector;
            PostingsOffset = -1;
            PostingsOffsets = postingsOffsets;
            VectorOffset = -1;
        }

        public VectorNode(Vector vector, long docId)
        {
            Vector = vector;
            PostingsOffset = -1;
            VectorOffset = -1;
            DocIds = new HashSet<long>();
            DocIds.Add(docId);
        }

        public VectorNode(long postingsOffset, long vecOffset, long terminator, long weight, long componentCount, Vector vector)
        {
            PostingsOffset = postingsOffset;
            VectorOffset = vecOffset;
            Terminator = terminator;
            Weight = weight;
            ComponentCount = componentCount;
            Vector = vector;
        }

        public VectorNode Detach()
        {
            _ancestor = null;
            _left = null;
            _right = null;
            _weight = 0;

            return this;
        }

        public void DetachFromAncestor()
        {
            _ancestor = null;
        }

        public string Visualize()
        {
            StringBuilder output = new StringBuilder();
            Visualize(this, output, 0);
            return output.ToString();
        }

        private void Visualize(VectorNode node, StringBuilder output, int depth)
        {
            if (node == null) return;
            output.Append('\t', depth);
            output.AppendFormat($"{node.AngleWhenAdded} {node} w:{node.Weight}");
            output.AppendLine();

            Visualize(node.Left, output, depth + 1);
            Visualize(node.Right, output, depth);
        }

        public override string ToString()
        {
            var vals = Vector.Values;

            var w = new StringBuilder();

            w.Append('|');

            for (int i = 0; i < Vector.Count;i++)
            {
                w.Append(vals[i]);

                w.Append('|');
            }

            w.Append(Vector.ToString());

            return w.ToString();
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
