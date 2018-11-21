using System;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Parses terms (key:value). Keys are separated from values by a ':' and
    /// terms are separated by newline characters. 
    /// Terms may be appended with a + sign (meaning AND), a - sign (meaning NOT) or nothing (meaning OR).
    /// </summary>
    public class KeyValueBooleanQueryParser
    {
        private static  char[] Operators = new char[] { ' ', '+', '-' };

        public Query Parse(string query, ITokenizer tokenizer)
        {
            Query root = null;
            Query previous = null;
            var lines = query.Split('\n');

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

                var values = key[0] == '_' ?
                    new[] { value } : 
                    tokenizer.Tokenize(value).ToArray();

                var or = root == null || (key[0] != '+' && key[0] != '-');
                var not = key[0] == '-';
                var and = !or && !not;

                if (Operators.Contains(key[0]))
                {
                    key = key.Substring(1);
                }

                var q = new Query { Term = new Term(key, values[0]), And = and, Or = or, Not = not };
                var qp = q;

                for (int i = 1; i < values.Length; i++)
                {
                    var next = new Query { Term = new Term(key, values[i]), Or = true };
                    qp.Next = next;
                    qp = qp.Next;
                }

                if (previous == null)
                {
                    root = q;
                    previous = q;
                }
                else
                {
                    previous.Next = q;
                    previous = q;
                }
            }

            return root;
        }
    }
}
