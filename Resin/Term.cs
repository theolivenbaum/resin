using System;
using System.Collections.Generic;

namespace Resin
{
    public class Term
    {
        private int _edits;

        public string Field { get; protected set; }
        public string Token { get; protected set; }

        public bool And { get; set; }
        public bool Not { get; set; }
        public bool Prefix { get; set; }
        public bool Fuzzy { get; set; }

        public IList<Term> Children { get; protected set; }

        public int Edits { get { return _edits; } }

        public float Similarity
        {
            set { _edits = Convert.ToInt32(Token.Length*(1-value)); }
        }

        public Term(string field, string token)
        {
            Field = field;
            Token = token;
        }

        public override string ToString()
        {
            var fldPrefix = And ? "+" : Not ? "-" : " ";
            var tokenSuffix = Prefix ? "*" : Fuzzy ? "~" : string.Empty;
            return string.Format("{0}{1}:{2}{3}", fldPrefix, Field, Token, tokenSuffix);
        }
    }
}