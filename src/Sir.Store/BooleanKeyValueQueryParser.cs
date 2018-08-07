using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Parses key:value clauses where keys are separated from values by a ':' and
    /// clauses are separated by newline characters. 
    /// Clauses may be appended with a + sign (meaning AND), a - sign (meaning NOT) or nothing (meaning OR).
    /// </summary>
    public class BooleanKeyValueQueryParser : IQueryParser
    {
        public string ContentType => "*";
        private static  char[] Operators = new char[] { ' ', '+', '-' };

        public Query Parse(string query, ITokenizer tokenizer)
        {
            Query root = null;
            var clauses = query.Split('\n');

            foreach (var clause in clauses)
            {
                var tokens = clause.Split(':');
                var key = tokens[0];
                var val = tokenizer.Normalize(tokens[1]);
                var and = root == null || key[0] == '+';
                var not = key[0] == '-';
                var or = !and && !not;

                if (Operators.Contains(key[0]))
                {
                    key = key.Substring(1);
                }

                var q = new Query { Term = new Term(key, val) };

                if (root == null)
                {
                    q.And = true;
                    root = q;
                }
                else
                {
                    if (and)
                    {
                        q.And = true;
                    }
                    else if (not)
                    {
                        q.Not = true;
                    }
                    else
                    {
                        q.Or = true;
                    }
                    root.Next = q;
                }
            }
            
            return root;
        }
        
        public void Dispose()
        {
        }
    }
}
