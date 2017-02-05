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

        public QueryContext(string field, string value) : base(field, value)
        {
            Children = new List<QueryContext>();
        }

        public IEnumerable<DocumentPosting> Reduce()
        {
            var first = Postings.ToList();

            foreach (var child in Children)
            {
                var other = child.Reduce().ToList();

                if (child.And)
                {
                    var dic =  other.ToDictionary(x => x.DocumentId);
                    var remainder = new List<DocumentPosting>();
                    foreach (var posting in first)
                    {
                        DocumentPosting exists;
                        if (dic.TryGetValue(posting.DocumentId, out exists))
                        {
                            posting.Combine(exists);
                            remainder.Add(posting);
                        }
                    }
                    first = remainder;
                }
                else if (child.Not)
                {
                    var dic = first.ToDictionary(x => x.DocumentId);
                    foreach (var posting in other)
                    {
                        DocumentPosting exists;
                        if (dic.TryGetValue(posting.DocumentId, out exists))
                        {
                            first.Remove(exists);
                        }
                    }
                }
                else // Or
                {
                    first = DocumentPosting.JoinOr(first, other).ToList();
                }
            }

            return first;
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