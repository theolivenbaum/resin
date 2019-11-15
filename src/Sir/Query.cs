using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// A boolean query,
    /// </summary>
    public class Query : BooleanStatement
    {
        public IList<Term> Terms { get; }
        public Query And { get; set; }
        public Query Or { get; set; }
        public Query Not { get; set; }

        public Query(
            IList<Term> terms, 
            bool and,
            bool or,
            bool not) : base(and, or, not)
        {
            Terms = terms;
        }
    }

    [System.Diagnostics.DebuggerDisplay("{Key}:{StringValue}")]
    public class Term : BooleanStatement
    {
        public IVector Vector { get; }
        public long KeyId { get; }
        public string Key { get; }
        public ulong CollectionId { get; }
        public IList<long> PostingsOffsets { get; set; }
        public double Score { get; set; }
        public string StringValue { get { return Vector.Data.ToString(); } }

        public Term(
            ulong collectionId,
            long keyId, 
            string key, 
            IVector vector, 
            bool and, 
            bool or, 
            bool not)
            : base(and, or, not)
        {
            CollectionId = collectionId;
            KeyId = keyId;
            Key = key;
            Vector = vector;
            Intersection = and;
            Union = or;
            Subtraction = not;
        }
    }

    public class BooleanStatement
    {
        private bool _and;
        private bool _or;
        private bool _not;

        public bool Intersection
        {
            get { return _and; }
            set
            {
                _and = value;

                if (value)
                {
                    Union = false;
                    Subtraction = false;
                }
            }
        }
        public bool Union
        {
            get { return _or; }
            set
            {
                _or = value;

                if (value)
                {
                    Intersection = false;
                    Subtraction = false;
                }
            }
        }
        public bool Subtraction
        {
            get { return _not; }
            set
            {
                _not = value;

                if (value)
                {
                    Intersection = false;
                    Union = false;
                }
            }
        }

        public BooleanStatement(bool and, bool or, bool not)
        {
            _and = and;
            _or = or;
            _not = not;
        }
    }
}