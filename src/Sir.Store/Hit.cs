namespace Sir.Store
{
    public class Hit
    {
        public double Score { get; set; }
        public VectorNode Node { get; set; }

        public Hit(VectorNode node, double score)
        {
            Score = score;
            Node = node;
        }

        public override string ToString()
        {
            return $"{Score} {Node}";
        }
    }
}
