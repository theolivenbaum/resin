namespace Sir.Store
{
    public class BooleanKeyValueQueryParser : IQueryParser
    {
        public string ContentType => "*";
        public int Ordinal => 0;

        public void Dispose()
        {
        }

        public Query Parse(string query)
        {
            var tokens = query.Split(new[] { ':' });
            if (tokens.Length > 1)
            {
                var key = tokens[0];
                var val = tokens[1];
                var q = new Query { Term = new Term(key, val) };
                Parse(q, tokens);
                return q;
            }
            return null;
        }

        private void Parse(Query query, string[] tokens)
        {
            for (int i = 2; i < tokens.Length; i++)
            {
                var key = tokens[i];
                var val = tokens[++i];
                var next = new Query
                {
                    Term = new Term(key, val)
                };

                var strVal = (string)next.Term.Value;
                if (strVal.StartsWith('-'))
                {
                    next.Term.Value = strVal.Substring(1, strVal.Length - 1);
                    query.Not = next;
                }
                else if (strVal.StartsWith('+'))
                {
                    next.Term.Value = strVal.Substring(1, strVal.Length - 1);
                    query.And = next;
                }
                else
                {
                    query.Or = next;
                }

                query = next;
            }
        }
    }
}
