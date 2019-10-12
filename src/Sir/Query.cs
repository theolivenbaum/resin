using System.Collections.Generic;
using System.Text;

namespace Sir
{
    /// <summary>
    /// A boolean query,
    /// </summary>
    public class Query : BooleanStatement
    {
        public IList<Clause> Clauses { get; private set; }

        public Query(long keyId, IList<Clause> clauses, bool and = false, bool or = true, bool not = false)
            : base(keyId, and, or, not)
        {
            Clauses = clauses;
        }

        public override string ToString()
        {
            var result = new StringBuilder();

            foreach (var clause in Clauses)
            {
                result.Append(clause.ToString());
            }

            var queryop = And ? "+" : Or ? "" : "-";

            return $"{queryop}{result}";
        }
    }

    public class Clause : BooleanStatement
    {
        public IVector Term { get; }
        public IList<long> PostingsOffsets { get; set; }
        public double Score { get; set; }

        public Clause(long keyId, IVector term, bool and = false, bool or = true, bool not = false)
            : base(keyId, and, or, not)
        {
            Term = term;
            And = and;
            Or = or;
            Not = not;
        }

        public override string ToString()
        {
            var queryop = And ? "+" : Or ? "" : "-";

            return $"{queryop}{KeyId}:{Term.ToString()}";
        }
    }

    public class BooleanStatement
    {
        private bool _and;
        private bool _or;
        private bool _not;

        public long KeyId { get; }
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

        public BooleanStatement(long keyId, bool and, bool or, bool not)
        {
            KeyId = keyId;
            _and = and;
            _or = or;
            _not = not;
        }
    }
}