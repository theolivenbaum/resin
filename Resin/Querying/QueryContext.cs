using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using log4net;
using Resin.IO;

namespace Resin.Querying
{
    public class QueryContext : QueryTerm
    {
        public IList<QueryContext> Children { get; set; }
        public IEnumerable<Term> Terms { get; set; }
        public IEnumerable<IEnumerable<DocumentScore>> Scores { get; set; }
        public IEnumerable<DocumentScore> Reduced { get; set; }
        private static readonly ILog Log = LogManager.GetLogger(typeof(QueryContext));

        public QueryContext(string field, string value) : base(field, value)
        {
            Children = new List<QueryContext>();
        }

        public IEnumerable<DocumentScore> Reduce()
        {
            var time = new Stopwatch();
            time.Start();

            var resolved = new ConcurrentDictionary<string, DocumentScore>(Reduced.ToDictionary(x => x.DocId, x => x));

            foreach(var child in Children)
            {
                if (child.And)
                {
                    foreach (var score in resolved.Values)
                    {
                        DocumentScore existing;

                        if (child.Reduce().ToDictionary(x => x.DocId, x => x).TryGetValue(score.DocId, out existing))
                        {
                            resolved.AddOrUpdate(score.DocId, score, (s, documentScore) => documentScore.Combine(score));
                        }
                        else
                        {
                            DocumentScore removed;
                            resolved.TryRemove(score.DocId, out removed);
                        }
                    }
                }
                else if (child.Not)
                {
                    foreach (var d in child.Reduce())
                    {
                        DocumentScore removed;
                        resolved.TryRemove(d.DocId, out removed);
                    }
                }
                else // Or
                {
                    foreach (var d in child.Reduce())
                    {
                        resolved.AddOrUpdate(d.DocId, d, (s, documentScore) => documentScore.Combine(d));
                    }
                }
            }

            Log.DebugFormat("reduced {0} in {1}", this, time.Elapsed);

            return resolved.Values;
        }

        public static IEnumerable<DocumentScore> JoinOr(IEnumerable<DocumentScore> first, IEnumerable<DocumentScore> second)
        {
            var resolved = new ConcurrentDictionary<string, DocumentScore>(first.ToDictionary(x => x.DocId, x => x));

            foreach (var score in second)
            {
                resolved.AddOrUpdate(score.DocId, score, (s, documentScore) => documentScore.Combine(score));
            }

            return resolved.Values;
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