using System.Collections.Generic;
using System.Diagnostics;

namespace Sir.VectorSpace
{
    [DebuggerDisplay("{Score} {Node}")]
    public class Hit
    {
        public double Score { get; set; }
        public VectorNode Node { get; set; }
        public IList<VectorNode> Path { get; set; }

        public Hit (VectorNode node, double score, IList<VectorNode> path)
        {
            Score = score;
            Node = node;
            Path = path;
        }

        public override string ToString()
        {
            return $"{Score} {Node}";
        }
    }
}
