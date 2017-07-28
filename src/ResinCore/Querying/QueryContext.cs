using System.Collections.Generic;
using System.Text;

namespace Resin.Querying
{
    public class QueryContext
    {
        public IList<DocumentScore> Scores { get; set; }
        public Query Query { get; set; }
    }

    public static class QueryContextHelper
    {
        public static IList<DocumentScore> Reduce(this IList<QueryContext> query)
        {
            var first = query[0].Scores;

            for (int i = 1; i < query.Count; i++)
            {
                var term = query[i];
                var other = term.Scores;

                if (term.Query.Or)
                {
                    first = DocumentScore.CombineOr(first, other);
                }
                else if (term.Query.Not)
                {
                    first = DocumentScore.Not(first, other);
                }
                else // And
                {
                    first = DocumentScore.CombineAnd(first, other);
                }
            }

            return first;
        }

        public static string ToQueryString(this IList<QueryContext> query)
        {
            var log = new StringBuilder();

            foreach (var q in query)
            {
                log.Append(q.Query.ToString());
            }

            return log.ToString();
        }

        public static bool TryCompress(QueryContext first, QueryContext second)
        {
            var field = first.Query.Field;
            var valLo = first.Query.Value;
            string valHi = null;

            if ((first.Query.GreaterThan || first.Query.LessThan) && 
                (second.Query.GreaterThan || second.Query.LessThan))
            {
                // compress a GT or LS with another LS or GT to create a range query

                if (second.Query.Field.Equals(first.Query.Field))
                {
                    if (first.Query.GreaterThan)
                    {
                        valHi = second.Query.Value;
                    }
                    else
                    {
                        valHi = first.Query.Value;
                        valLo = second.Query.Value;
                    }

                    first.Query = new RangeQuery(field, valLo, valHi);
                    return true;
                }
            }
            return false;
        }
    }
}