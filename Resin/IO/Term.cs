using System;

namespace Resin.IO
{
    //TODO:remove from IO namespace
    [Serializable]
    public class Term
    {
        private int _edits;

        public string Field { get; protected set; }
        public string Value { get; protected set; }

        public bool And { get; set; }
        public bool Not { get; set; }
        public bool Prefix { get; set; }
        public bool Fuzzy { get; set; }

        public int Edits { get { return _edits; } }

        public float Similarity
        {
            set { _edits = Convert.ToInt32(Value.Length*(1-value)); }
        }

        public Term(string field, string value)
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