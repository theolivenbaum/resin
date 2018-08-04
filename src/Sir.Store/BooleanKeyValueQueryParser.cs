using System;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Parses key:value clauses that are separated by a newline characters into <see cref="Sir.Term"/>s . 
    /// Clauses may be appended with a + sign (meaning AND), a - sign (meaning NOT) or 
    /// a space (meaning OR).
    /// </summary>
    public class BooleanKeyValueQueryParser : IQueryParser
    {
        public string ContentType => "*";

        public Query Parse(string query)
        {
            Query root = null;
            var clauses = query.Split('\n');

            foreach (var clause in clauses)
            {
                var tokens = clause.Split(':');
                var key = tokens[0];
                var val = tokens[1];
                var and = root == null || key[0] == '+';
                var not = key[0] == '-';

                if (root != null)
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
                        root.Not = true;
                    }
                    else
                    {
                        root.Or = true;
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
