using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// A boolean query,
    /// </summary>
    public class Query
    {
        public IList<Term> Terms { get; }
        public Query And { get; set; }
        public Query Or { get; set; }
        public Query Not { get; set; }

        public Query(IList<Term> terms)
        {
            Terms = terms;
        }
    }

    public class Term : BooleanStatement
    {
        public IVector Vector { get; }
        public long KeyId { get; }
        public string Key { get; }
        public ulong CollectionId { get; }

        public IList<long> PostingsOffsets { get; set; }
        public double Score { get; set; }

        public Term(
            ulong collectionId,
            long keyId, 
            string key, 
            IVector term, 
            bool and = false, 
            bool or = true, 
            bool not = false)
            : base(and, or, not)
        {
            CollectionId = collectionId;
            KeyId = keyId;
            Key = key;
            Vector = term;
            And = and;
            Or = or;
            Not = not;
        }
    }

    public class BooleanStatement
    {
        private bool _and;
        private bool _or;
        private bool _not;

        public bool And
        {
            get { return _and; }
            set
            {
                _and = value;

                if (value)
                {
                    Or = false;
                    Not = false;
                }
            }
        }
        public bool Or
        {
            get { return _or; }
            set
            {
                _or = value;

                if (value)
                {
                    And = false;
                    Not = false;
                }
            }
        }
        public bool Not
        {
            get { return _not; }
            set
            {
                _not = value;

                if (value)
                {
                    And = false;
                    Or = false;
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