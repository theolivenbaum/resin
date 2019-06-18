using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public Query(ulong collectionId, Term term, bool and = false, bool or = true, bool not = false)
        {
            Term = term;
            PostingsOffsets = new List<long>();
            And = and;
            Or = or;
            Not = not;
            Collection = collectionId;
        }

        public Query(ulong collectionId, float score, IList<long> postingsOffsets, bool and = false, bool or = true, bool not = false)
        {
            Score = score;
            PostingsOffsets = postingsOffsets;
            Or = true;
            Collection = collectionId;
        }

        public ulong Collection { get; private set; }
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
        public Term Term { get; private set; }
        public Query NextClause { get; set; }
        public Query NextTermInClause { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public IList<long> PostingsOffsets { get; set; }
        public float Score { get; set; }

        public override string ToString()
        {
            var result = new StringBuilder();
            var query = this;

            while (query != null)
            {
                var termResult = new StringBuilder();
                var term = query;
                var queryop = query.And ? "+" : query.Or ? string.Empty : "-";
                var termop = term.And ? "+" : term.Or ? string.Empty : "-";

                while (term != null)
                {
                    termResult.AppendFormat("{0}{1} ", termop, term.Term);

                    term = term.NextTermInClause;
                }

                result.AppendFormat("{0}({1})\n", queryop, termResult.ToString().TrimEnd());

                query = query.NextClause;
            
            }

            return result.ToString();
        }

        public string ToDiagram()
        {
            var x = this;
            var diagram = new StringBuilder();

            while (x != null)
            {
                diagram.AppendLine(x.ToString());

                x = x.NextClause;
            }

            return diagram.ToString();
        }

        public IList<Query> ToClauses()
        {
            var list = new List<Query>();
            Query q = this;

            while (q != null)
            {
                list.Add(q);
                q = q.NextClause;
            }

            return list;
        }

        public Query AddClause(Query query)
        {
            if (NextTermInClause == null)
            {
                NextTermInClause = query;
            }
            else
            {
                NextTermInClause.AddClause(query);
            }

            return this;
        }
    }
}