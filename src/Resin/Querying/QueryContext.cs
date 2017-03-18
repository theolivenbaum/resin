using System.Collections.Generic;
using System.Linq;
using System.Text;
using Resin.IO;

namespace Resin.Querying
{
    public class QueryContext : QueryTerm
    {
        public IList<QueryContext> Children { get; set; }
        public IEnumerable<Term> Terms { get; set; }
        public IEnumerable<DocumentPosting> Postings { get; set; }
        public IEnumerable<DocumentScore> Scored { get; set; }

        public QueryContext(string field, string value) : base(field, value)
        {
            Children = new List<QueryContext>();
        }

        public IEnumerable<DocumentScore> Reduce()
        {
            var first = Scored.ToList();

            foreach (var child in Children)
            {
                var other = child.Reduce().ToList();

                if (child.And)
                {
                    first = DocumentScore.CombineAnd(first, other).ToList();
                }
                else if (child.Not)
                {
                    var dic = first.ToDictionary(x => x.DocumentId);
                    foreach (var posting in other)
                    {
                        DocumentScore exists;
                        if (dic.TryGetValue(posting.DocumentId, out exists))
                        {
                            first.Remove(exists);
                        }
                    }
                }
                else // Or
                {
                    first = DocumentScore.CombineOr(first, other).ToList();
                }
            }

            return first;
        }

        public QueryContext Clone()
        {
            return new QueryContext(Field, Value) {Children = Children, And = And, Not = Not, Edits = Edits, Fuzzy = Fuzzy, Prefix = Prefix};
        }
      
        public override string ToString()
        {
            var s = new StringBuilder();
            s.AppendFormat(base.ToString());

            foreach (var child in Children)
            {
                s.AppendFormat(" {0}", child);
            }
            return s.ToString();
        }
    }
}