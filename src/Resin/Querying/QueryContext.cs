using System.Collections.Generic;
using System.Linq;
using System.Text;
using Resin.IO;

namespace Resin.Querying
{
    public class QueryContext : QueryTerm
    {
        public IEnumerable<Term> Terms { get; set; }
        public IEnumerable<DocumentPosting> Postings { get; set; }
        public IEnumerable<DocumentScore> Scored { get; set; }

        protected QueryContext Next { get { return _queries == null ? null : _queries.FirstOrDefault(); } }

        private IList<QueryContext> _queries;
 
        public QueryContext(string field, string value) : base(field, value)
        {
        }

        public IList<QueryContext> ToList()
        {
            return YieldAll().ToList();
        }

        private IEnumerable<QueryContext> YieldAll()
        {
            yield return this;
            foreach (var q in _queries) yield return q;
        } 

        public IEnumerable<DocumentScore> Reduce()
        {
            if (Next == null)
            {
                return Scored;
            }

            var next = Next.Reduce().ToList();

            if (Next.And)
            {
                return DocumentScore.CombineAnd(Scored, next);
            }
            if (Next.Not)
            {
                return DocumentScore.Not(Scored, next);
            }

            return DocumentScore.CombineOr(Scored, next);
        }

        public void Add(QueryContext queryContext)
        {
            if (_queries == null) _queries = new List<QueryContext>();

            _queries.Add(queryContext);
        }

        public override string ToString()
        {
            var s = new StringBuilder();

            s.AppendFormat(base.ToString());

            if (Next != null) s.AppendFormat(" {0}", Next);

            return s.ToString();
        }
    }
}