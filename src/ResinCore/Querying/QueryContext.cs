using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentTable;
using System;

namespace Resin.Querying
{
    public class QueryContext : Query
    {
        public IList<Term> Terms { get; set; }
        public IList<DocumentPosting> Postings { get; set; }
        public IEnumerable<DocumentScore> Scored { get; set; }

        private IList<QueryContext> _queries = new List<QueryContext>();
 
        public QueryContext(string field, string value) : base(field, value)
        {
        }

        public QueryContext(string field, long value) : base(field, value)
        {
        }

        public QueryContext(string field, DateTime value) : base(field, value)
        {
        }

        public QueryContext(string field, string value, string upperBound)
            : base(field, value, upperBound)
        {
        }

        public QueryContext(string field, long value, long upperBound)
            : base(field, value, upperBound)
        {
        }

        public QueryContext(string field, DateTime value, DateTime upperBound)
            : base(field, value, upperBound)
        {
        }

        public IList<QueryContext> ToList()
        {
            return YieldAll().ToList();
        }

        private IEnumerable<QueryContext> YieldAll()
        {
            yield return this;

            foreach (var q in _queries)
            {
                foreach (var sq in q.YieldAll()) yield return sq;
            }
        } 

        public IEnumerable<DocumentScore> Reduce()
        {
            var first = Scored;

            if (_queries != null)
            {
                foreach (var child in _queries)
                {
                    var other = child.Reduce();

                    if (child.And)
                    {
                        first = DocumentScore.CombineAnd(first, other).ToList();
                    }
                    else if (child.Not)
                    {
                        first = DocumentScore.Not(first, other).ToList();
                    }
                    else // Or
                    {
                        first = DocumentScore.CombineOr(first, other).ToList();
                    }
                } 
            }

            return first;
        }

        public void Add(QueryContext queryContext)
        {
            if (_queries == null) _queries = new List<QueryContext>();

            if ((GreaterThan || LessThan) && (queryContext.GreaterThan || queryContext.LessThan))
            {
                // compress a GT or LS with another LS or GT to create a range query
                if (queryContext.Field.Equals(Field))
                {
                    Range = true;
                }

                if (GreaterThan)
                {
                    ValueUpperBound = queryContext.Value;
                }
                else
                {
                    ValueUpperBound = Value;
                    Value = queryContext.Value;
                }

                GreaterThan = false;
                LessThan = false;
            }
            else
            {
                _queries.Add(queryContext);
            }
        }

        public override string ToString()
        {
            var log = new StringBuilder();

            log.Append(base.ToString());

            if (_queries != null && _queries.Count > 0)
            {
                log.Append(' ');

                var entries = new List<string>();

                foreach (var q in _queries)
                {
                    entries.Add(q.ToString());
                    entries.Add(" ");
                }

                foreach (var e in entries.Take(entries.Count-1))
                {
                    log.Append(e);
                }
            }
            
            return log.ToString();
        }
    }
}