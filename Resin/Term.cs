using System;

namespace Resin
{
    public class Term
    {
        private int _edits;

        public string Field { get; set; }
        public string Token { get; set; }

        public bool And { get; set; }
        public bool Not { get; set; }
        public bool Prefix { get; set; }
        public bool Fuzzy { get; set; }

        public int Edits { get { return _edits; } }

        public float Similarity
        {
            set { _edits = Convert.ToInt32(Token.Length*(1-value)); }
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Field, Token);
        }
    }
}