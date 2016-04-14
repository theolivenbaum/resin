using System.Collections.Generic;
using System.Linq;
using System.Text;
using Resin.IO;

namespace Resin
{
    public class Query : Term
    {
        public IList<Query> Children { get; protected set; }
        public IDictionary<string, DocumentScore> Result { get; set ; }
        public FieldFile FieldFile { get; set; }

        public Query(string field, string token) : base(field, token)
        {
            Children = new List<Query>();
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