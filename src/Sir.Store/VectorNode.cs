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
        public const int BlockSize = sizeof(long) + sizeof(long) + sizeof(int) + sizeof(int) + sizeof(byte);
        public const int ComponentSize = sizeof(long) + sizeof(int);

        private VectorNode _right;
        private VectorNode _left;
        private VectorNode _ancestor;
        private int _weight;
        private float _angleWhenAdded;
        private object _sync = new object();

        public HashSet<long> DocIds { get; set; }

        public int ComponentCount { get; set; }
        public long VectorOffset { get; set; }
        public long PostingsOffset { get; set; }
        public SortedList<long, int> Vector { get; set; }
        public object Sync { get { return _sync; } }

        public int Weight
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

        public VectorNode Ancestor
        {
            get { return _ancestor; }
        }

        public byte Terminator { get; set; }

        public IList<long> PostingsOffsets { get; set; }
        public float AngleWhenAdded { get => _angleWhenAdded; set => _angleWhenAdded = value; }

        public VectorNode()
            : this('\0'.ToString())
        {
        }

        public VectorNode(string s)
            : this(s.ToVector())
        {
        }

        public VectorNode(SortedList<long, int> termVector)
        {
            Vector = termVector;
            PostingsOffset = -1;
            VectorOffset = -1;
        }

        public VectorNode(SortedList<long, int> vector, long docId)
        {
            Vector = vector;
            PostingsOffset = -1;
            VectorOffset = -1;
            DocIds = new HashSet<long>();
            DocIds.Add(docId);
        }

        public VectorNode(long postingsOffset, long vecOffset, byte terminator, int weight, int componentCount)
        {
            PostingsOffset = postingsOffset;
            VectorOffset = vecOffset;
            Terminator = terminator;
            Weight = weight;
            ComponentCount = componentCount;
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
            var w = new StringBuilder();

            foreach (var c in Vector)
            {
                w.Append(c.Key);
                w.Append('.');
            }

            return w.ToString();
        }
    }
}
