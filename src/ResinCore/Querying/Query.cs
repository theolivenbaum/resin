using System;
using System.Collections.Generic;

namespace Resin.Querying
{
    public abstract class Query
    {
        public float Similarity { get; set; }
        public string Field { get; protected set; }
        public string Value { get; set; }

        public bool Or { get; set; }
        public bool Not { get; set; }
        public bool Prefix { get; set; }
        public bool Fuzzy { get; set; }

        public bool GreaterThan { get; set; }
        public bool LessThan { get; set; }

        public int Edits(string word)
        {
            return Convert.ToInt32(Math.Floor(word.Length * (1 - Similarity)));
        }

        public Query(string field, string value)
        {
            Field = field;
            Value = value;
        }

        public Query(string field, long value)
            : this(field, value.ToString().PadLeft(19, '0'))
        {
        }

        public Query(string field, DateTime value)
            : this(field, value.ToUniversalTime().Ticks)
        {
        }
        
        public override string ToString()
        {
            return Serialize();
        }

        public virtual string Serialize()
        {
            var fldPrefix = Or ? " " : Not ? "-" : "+";
            var tokenSuffix = Prefix ? "*" : Fuzzy ? "~" : string.Empty;

            var delimiter = ":";

            if (GreaterThan) delimiter = ">";
            else if (LessThan) delimiter = "<";

            var val = Value;

            return string.Format("{0}{1}{2}{3}{4}",
                fldPrefix, Field, delimiter, val, tokenSuffix);
        }
    }

    public class TermQuery : Query
    {
        public TermQuery(string field, string value) 
            : base(field, value)
        {
        }
        public TermQuery(string field, long value)
            : base(field, value)
        {
        }
        public TermQuery(string field, DateTime value)
            : base(field, value)
        {
        }
    }

    public class PhraseQuery : Query
    {
        public IList<string> Values { get; set; }

        public PhraseQuery(string field, IList<string> values)
            : base (field, null)
        {
            Values = values;
        }

        public override string Serialize()
        {
            var fldPrefix = Or ? " " : Not ? "-" : "+";
            var tokenSuffix = Prefix ? "*" : Fuzzy ? "~" : string.Empty;

            var delimiter = ":";

            if (GreaterThan) delimiter = ">";
            else if (LessThan) delimiter = "<";

            var val = string.Join(" ", Values);

            return string.Format("{0}{1}{2}{3}{4}",
                fldPrefix, Field, delimiter, val, tokenSuffix);
        }
    }

    public class RangeQuery : Query
    {
        public string ValueUpperBound { get; set; }

        public RangeQuery(string field, string value)
            : base(field, value)
        {
        }

        public RangeQuery(string field, string value, string valueUpperBound)
            : base(field, value)
        {
            ValueUpperBound = valueUpperBound;
        }

        public RangeQuery(string field, long value, long valueUpperBound)
            : this(field, value.ToString().PadLeft(19, '0'), valueUpperBound.ToString().PadLeft(19, '0'))
        {
        }

        public RangeQuery(string field, DateTime value, DateTime valueUpperBound)
            : this(field, value.ToUniversalTime().Ticks, valueUpperBound.ToUniversalTime().Ticks)
        {
        }

        public override string Serialize()
        {
            var fldPrefix = Or ? " " : Not ? "-" : "+";
            var tokenSuffix = Prefix ? "*" : Fuzzy ? "~" : string.Empty;

            var s = string.Format("{0}{1}>{2}{3} {1}<{4}",
                    fldPrefix, Field, Value, tokenSuffix, ValueUpperBound);
            return s;
        }
    }
}