using System;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Parses terms ([key]:[value]). 
    /// Terms are enclosed in parenthases and separated by space. 
    /// Keys are separated from values by a ':'.
    /// Terms may be appended with a + sign (meaning AND), a - sign (meaning NOT) or nothing (meaning OR).
    /// </summary>
    public class QueryParser
    {
        private static  char[] Operators = new char[] { ' ', '+', '-' };

        public Query Parse(ulong collectionId, string query, IStringModel tokenizer)
        {
            Query root = null;
            Query cursor = null;
            var lines = query
                .Replace("\r", "\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.IndexOf(':', 0, line.Length) < 0)
                {
                    throw new ArgumentException(
                        "Query syntax error. A query must define both a key and a value separated by a colon.", nameof(query));
                }

                var parts = line.Split(':');
                var key = parts[0];
                var value = parts[1];
                var values = key[0] == '_' 
                    ? new AnalyzedComputerString(new List<Vector> { value.ToIndexedVector(0, value.Length)})
                    : tokenizer.Tokenize(value);
                var or = key[0] != '+' && key[0] != '-';
                var not = key[0] == '-';
                var and = !or && !not;

                if (Operators.Contains(key[0]))
                {
                    key = key.Substring(1);
                }

                var q = new Query(collectionId, new Term(key, values, 0)) { And = and, Or = or, Not = not };
                var qc = q;

                for (int i = 1; i < values.Embeddings.Count; i++)
                {
                    qc.NextTermInClause = new Query(collectionId, new Term(key, values, i)) { And = and, Or = or, Not = not };
                    qc = qc.NextTermInClause;
                }

                if (cursor == null)
                {
                    root = q;
                }
                else
                {
                    var last = cursor;
                    var next = last.NextClause;

                    while (next != null)
                    {
                        last = next;
                        next = last.NextClause;
                    }

                    last.NextClause = q;
                }

                cursor = q;
            }

            return root;
        }
    }
}
