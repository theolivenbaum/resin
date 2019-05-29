namespace Sir.Store
{
    public class Hit
    {
        public float Score { get; set; }
        public VectorNode Node { get; set; }

        public override string ToString()
        {
            return $"{Score} {Node}";
        }
    }
}
