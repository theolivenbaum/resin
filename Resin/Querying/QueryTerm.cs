using System;

namespace Resin.Querying
{
    public class QueryTerm
    {
        private int _edits;

        public string Field { get; protected set; }
        public string Value { get; protected set; }

        public bool And { get; set; }
        public bool Not { get; set; }
        public bool Prefix { get; set; }
        public bool Fuzzy { get; set; }

        public int Edits { get { return _edits; } set { _edits = value; }}

        public float Similarity
        {
            set { _edits = (int)Math.Ceiling(Value.Length*(1-value)); }
        }

        public QueryTerm(string field, string value)
        {
            Field = field;
            Value = value;
        }

        public override string ToString()
        {
            var fldPrefix = And ? "+" : Not ? "-" : string.Empty;
            var tokenSuffix = Prefix ? "*" : Fuzzy ? "~" : string.Empty;
            return string.Format("{0}{1}:{2}{3}", fldPrefix, Field, Value, tokenSuffix);
        }

        
    }
}