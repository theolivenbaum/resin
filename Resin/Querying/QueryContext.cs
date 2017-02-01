using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Resin.Querying
{
    public class QueryContext : QueryTerm
    {
        public IList<QueryContext> Children { get; protected set; }
        public IDictionary<string, DocumentScore> Result { get; set ; }

        public QueryContext(string field, string value) : base(field, value)
        {
            Children = new List<QueryContext>();
        }

        public IDictionary<string, DocumentScore> Resolve()
        {
            var result = new ConcurrentDictionary<string, DocumentScore>(Result);

            foreach(var child in Children)
            {
                if (child.And)
                {
                    var childResult = child.Resolve();
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
                    foreach (var d in child.Resolve())
                    {
                        DocumentScore removed;
                        result.TryRemove(d.Key, out removed);
                    }
                }
                else // Or
                {
                    foreach (var d in child.Resolve())
                    {
                        result.AddOrUpdate(d.Key, d.Value, (s, documentScore) => documentScore.Combine(d.Value));
                    }
                }
            }

            return result;
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

        public QueryTerm ToQueryTerm()
        {
            return new QueryTerm(Field, Value){Edits = Edits, And = And, Fuzzy = Fuzzy, Not = Not, Prefix = Prefix};
        }
    }
}