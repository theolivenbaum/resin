using System.Collections.Generic;
using System.Linq;
using System.Text;
using Resin.IO;

namespace Resin.Querying
{
    public class QueryContext : QueryTerm
    {
        public QueryContext Next { get; set; }
        public IEnumerable<Term> Terms { get; set; }
        public IEnumerable<DocumentPosting> Postings { get; set; }
        public IEnumerable<DocumentScore> Scored { get; set; }

        public QueryContext(string field, string value) : base(field, value)
        {
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
                var dic = Scored.ToDictionary(x => x.DocumentId);
                foreach (var score in next)
                {
                    DocumentScore exists;
                    if (dic.TryGetValue(score.DocumentId, out exists))
                    {
                        dic.Remove(exists.DocumentId);
                    }
                }
                return dic.Values;
            }
            return DocumentScore.CombineOr(Scored, next);
        }
      
        public override string ToString()
        {
            var s = new StringBuilder();

            s.AppendFormat(base.ToString());

            if(Next!=null) s.AppendFormat(" {0}", Next);

            return s.ToString();
        }

        public IList<QueryContext> ToList()
        {
            return ToListInternal().ToList();
        }

        private IEnumerable<QueryContext> ToListInternal()
        {
            yield return this;

            if (Next == null) yield break;
            
            foreach (var q in Next.ToList())
            {
                yield return q;
            }
        }

        public void Add(QueryContext queryContext)
        {
            var parent = this;

            while (parent.Next != null)
            {
                parent = parent.Next;
            }

            parent.Next = queryContext;
        }
    }
}