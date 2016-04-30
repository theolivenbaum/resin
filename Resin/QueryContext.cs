using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Resin
{
    public class QueryContext : Term
    {
        public IList<QueryContext> Children { get; protected set; }
        public IDictionary<string, DocumentScore> Result { get; set ; }

        public QueryContext(string field, string value) : base(field, value)
        {
            Children = new List<QueryContext>();
        }

        public IDictionary<string, DocumentScore> Resolve()
        {
            var result = Result;
            foreach (var child in Children)
            {
                if (child.And)
                {
                    var childResult = child.Resolve();
                    var docs = result.Values.ToList();
                    foreach (var d in docs)
                    {
                        DocumentScore score;
                        if (childResult.TryGetValue(d.DocId, out score))
                        {
                            result[d.DocId] = score.Add(d);
                        }
                        else
                        {
                            result.Remove(d.DocId);
                        }
                    }
                }
                else if (child.Not)
                {
                    foreach (var d in child.Resolve())
                    {
                        result.Remove(d.Key);
                    }
                }
                else // Or
                {
                    foreach (var d in child.Resolve())
                    {
                        DocumentScore existingScore;
                        if (result.TryGetValue(d.Key, out existingScore))
                        {
                            result[d.Key] = existingScore.Add(d.Value);
                        }
                        else
                        {
                            result.Add(d);
                        }
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
    }
}