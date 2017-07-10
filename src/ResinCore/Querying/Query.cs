using System;

namespace Resin.Querying
{
    public class Query : IEquatable<Query>
    {
        private int _edits;

        public string Field { get; protected set; }
        public string Value { get; protected set; }
        public string ValueUpperBound { get; set; }

        public bool And { get; set; }
        public bool Not { get; set; }
        public bool Prefix { get; set; }
        public bool Fuzzy { get; set; }

        public bool GreaterThan { get; set; }
        public bool LessThan { get; set; }
        public bool Range { get; set; }

        public int Edits
        {
            get
            {
                return _edits;
            }
            set
            {
                _edits = value;

                if (Fuzzy && Edits == 0)
                {
                    Fuzzy = false;
                }
            }
        }

        public float Similarity
        {
            set
            {
                _edits = Convert.ToInt32(Math.Floor(Value.Length * (1 - value)));

                if (Fuzzy && Edits == 0)
                {
                    Fuzzy = false;
                }
            }
        }

        public Query(string field, string value)
        {
            Field = field;
            Value = value;
            And = true;
        }

        public Query(string field, string value, string valueUpperBound)
        {
            Field = field;
            Value = value;
            ValueUpperBound = valueUpperBound;
            And = true;
            Range = true;
        }

        public Query(string field, long value)
            : this(field, value.ToString().PadLeft(19, '0'))
        {
        }

        public Query(string field, long value, long valueUpperBound)
            : this(field, value.ToString().PadLeft(19, '0'), valueUpperBound.ToString().PadLeft(19, '0'))
        {
        }

        public Query(string field, DateTime value)
            : this(field, value.ToUniversalTime().Ticks)
        {
        }

        public Query(string field, DateTime value, DateTime valueUpperBound)
            : this(field, value.ToUniversalTime().Ticks, valueUpperBound.ToUniversalTime().Ticks)
        {
        }

        public override string ToString()
        {
            return Serialize();
        }

        public string Serialize()
        {
            var fldPrefix = And ? "+" : Not ? "-" : string.Empty;
            var tokenSuffix = Prefix ? "*" : Fuzzy ? "~" : string.Empty;

            if (Range)
            {
                var s = string.Format("{0}{1}>{2}{3} {1}<{4}", 
                    fldPrefix, Field, Value, tokenSuffix, ValueUpperBound);
                return s;
            }
            else
            {
                var delimiter = ":";

                if (GreaterThan) delimiter = ">";
                else if (LessThan) delimiter = "<";

                return string.Format("{0}{1}{2}{3}{4}", 
                    fldPrefix, Field, delimiter, Value, tokenSuffix);
            }
        }

        public bool Equals(Query other)
        {
            if (other == null) return false;

            return other.Field == Field && other.Value == Value;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Field.GetHashCode();
                hash = hash * 23 + Value.GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Query);
        }
    }
}