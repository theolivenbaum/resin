namespace Resin
{
    public class Term
    {
        private int _boost;
        public string Field { get; set; }
        public string Token { get; set; }

        public bool And { get; set; }
        public bool Not { get; set; }
        public bool Prefix { get; set; }
        public bool Fuzzy { get; set; }

        internal int InternalBoost { get; set; }

        public int Boost
        {
            get { return _boost; }
            set { if (value > 0) _boost = value; }
        }

        public Term()
        {
            InternalBoost = 1;
            Boost = 1;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Field, Token);
        }
    }
}