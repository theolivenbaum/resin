namespace Sir.Store
{
    public class Hit
    {
        public double Score { get; set; }
        public VectorNode Node { get; set; }

        public override string ToString()
        {
            return $"{Score} {Node}";
        }
    }
}
