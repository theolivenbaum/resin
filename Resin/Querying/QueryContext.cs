using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Resin.Querying
{
    public class QueryContext : QueryTerm
    {
        public IList<QueryContext> Children { get; set; }
        public IEnumerable<DocumentScore> Result { get; set ; }

        public QueryContext(string field, string value) : base(field, value)
        {
            Children = new List<QueryContext>();
        }

        public IEnumerable<DocumentScore> Reduce()
        {
            var result = new ConcurrentDictionary<string, DocumentScore>(Result.ToDictionary(x => x.DocId, x => x));

            foreach(var child in Children)
            {
                var childResult = child.Reduce().ToDictionary(x=>x.DocId, x=>x);

                if (child.And)
                {
                    var scores = result.Values;

                    foreach (var score in scores)
                    {
                        DocumentScore existing;

                        if (childResult.TryGetValue(score.DocId, out existing))
                        {
                            result.AddOrUpdate(score.DocId, score, (s, documentScore) => documentScore.Combine(score));
                        }
                        else
                        {
                            DocumentScore removed;
                            result.TryRemove(score.DocId, out removed);
                        }
                    }
                }
                else if (child.Not)
                {
                    foreach (var d in childResult)
                    {
                        DocumentScore removed;
                        result.TryRemove(d.Key, out removed);
                    }
                }
                else // Or
                {
                    foreach (var d in childResult)
                    {
                        result.AddOrUpdate(d.Key, d.Value, (s, documentScore) => documentScore.Combine(d.Value));
                    }
                }
            }

            return result.Values;
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