namespace Resin
{
    public class Term
    {
        public string Field { get; set; }
        public string Token { get; set; }
        public bool And { get; set; }
        public bool Not { get; set; }
        public bool Prefix { get; set; }
        public bool Fuzzy { get; set; }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Field, Token);
        }
    }
}