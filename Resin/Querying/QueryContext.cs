using System.Collections.Concurrent;
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
        public IEnumerable<DocumentPosting> Reduced { get; set; }
        public IEnumerable<DocumentScore> Scores { get; set; }

        public QueryContext(string field, string value) : base(field, value)
        {
            Children = new List<QueryContext>();
        }

        public IEnumerable<DocumentPosting> Reduce()
        {
            var reduced = new ConcurrentDictionary<string, DocumentPosting>(Postings.ToDictionary(x => x.DocumentId, x => x));

            foreach (var child in Children)
            {
                var join = child.Reduce().ToDictionary(x => x.DocumentId, x => x);

                if (child.And)
                {
                    foreach (var posting in reduced.Values)
                    {
                        DocumentPosting existing;

                        if (join.TryGetValue(posting.DocumentId, out existing))
                        {
                            reduced.AddOrUpdate(posting.DocumentId, posting, (s, p) => p.Combine(posting));
                        }
                        else
                        {
                            reduced.TryRemove(posting.DocumentId, out existing);
                        }
                    }
                }
                else if (child.Not)
                {
                    foreach (var posting in join.Values)
                    {
                        DocumentPosting removed;
                        reduced.TryRemove(posting.DocumentId, out removed);
                    }
                }
                else // Or
                {
                    foreach (var posting in join.Values)
                    {
                        reduced.AddOrUpdate(posting.DocumentId, posting, (s, p) => p.Combine(posting));
                    }
                }
            }

            return reduced.Values;
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