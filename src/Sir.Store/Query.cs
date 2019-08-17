using System.Collections.Generic;
using System.Text;

namespace Sir.Store
{
    /// <summary>
    /// A boolean query,
    /// </summary>
    public class Query
    {
        private bool _and;
        private bool _or;
        private bool _not;

        public Query(ulong collectionId, long keyId, AnalyzedData terms, bool and = false, bool or = true, bool not = false)
        {
            Terms = terms;
            PostingsOffsets = new List<long>();
            And = and;
            Or = or;
            Not = not;
            CollectionId = collectionId;
            KeyId = keyId;
        }

        public Query Copy(IVector vector)
        {
            return new Query(CollectionId, KeyId, new AnalyzedData(new IVector[1] { vector }), And, Or, Not);
        }

        public long KeyId { get; }
        public ulong CollectionId { get; private set; }
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
        public AnalyzedData Terms { get; private set; }
        public IList<long> PostingsOffsets { get; set; }
        public double Score { get; set; }
        public int TermCount { get { return Terms.Embeddings.Count; } }

        public override string ToString()
        {
            var result = new StringBuilder();

            foreach (var term in Terms.Embeddings)
            {
                var queryop = And ? "+" : Or ? "" : "-";

                result.AppendFormat("{0}{1} ", queryop, term.ToString());
            }

            return result.ToString().TrimEnd();
        }
    }
}