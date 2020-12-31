using System.Collections.Generic;

namespace Sir.VectorSpace
{
    /// <summary>
    /// A boolean query.
    /// </summary>
    /// <example>
    /// {
    ///	        "or":{
    ///		        "collection":"wikipedia",
    ///		        "title":"ferriman gallwey score"
    ///     }
    /// }
    /// </example>
    public class Query : BooleanStatement
    {
        public IList<Term> Terms { get; }
        public HashSet<string> Select { get; }
        public Query And { get; set; }
        public Query Or { get; set; }
        public Query Not { get; set; }

        public Query(
            IList<Term> terms,
            IEnumerable<string> select,
            bool and,
            bool or,
            bool not) : base(and, or, not)
        {
            Terms = terms;
            Select = new HashSet<string>(select);
        }

        public int TotalNumberOfTerms()
        {
            var count = Terms.Count;

            if (And != null)
            {
                count += And.TotalNumberOfTerms();
            }
            if (Or != null)
            {
                count += Or.TotalNumberOfTerms();
            }
            if (Not != null)
            {
                count += Not.TotalNumberOfTerms();
            }

            return count;
        }

        public int GetCollectionCount()
        {
            var dic = new HashSet<ulong>();

            GetNumOfCollections(dic);

            return dic.Count;
        }

        public void GetNumOfCollections(HashSet<ulong> dic)
        {
            foreach (var term in Terms)
            {
                dic.Add(term.CollectionId);
            }

            if (And != null)
            {
                And.GetNumOfCollections(dic);
            }
            if (Or != null)
            {
                Or.GetNumOfCollections(dic);
            }
            if (Not != null)
            {
                Not.GetNumOfCollections(dic);
            }
        }

        public IEnumerable<Term> AllTerms()
        {
            foreach (var q in All())
                foreach (var term in q.Terms)
                    yield return term;
        }

        public IEnumerable<Query> All()
        {
            yield return this;

            if (And != null)
            {
                foreach (var q in And.All())
                    yield return q;
            }
            if (Or != null)
            {
                foreach (var q in Or.All())
                    yield return q;
            }
            if (Not != null)
            {
                foreach (var q in Not.All())
                    yield return q;
            }
        }
    }

    public class BooleanStatement
    {
        private bool _and;
        private bool _or;
        private bool _not;

        public bool IsIntersection
        {
            get { return _and; }
            set
            {
                _and = value;

                if (value)
                {
                    IsUnion = false;
                    IsSubtraction = false;
                }
            }
        }
        public bool IsUnion
        {
            get { return _or; }
            set
            {
                _or = value;

                if (value)
                {
                    IsIntersection = false;
                    IsSubtraction = false;
                }
            }
        }
        public bool IsSubtraction
        {
            get { return _not; }
            set
            {
                _not = value;

                if (value)
                {
                    IsIntersection = false;
                    IsUnion = false;
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