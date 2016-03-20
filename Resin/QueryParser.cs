using System.Collections.Generic;
using System.Linq;

namespace Resin
{
    public class QueryParser
    {
        private readonly IAnalyzer _analyzer;

        public QueryParser(IAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public IEnumerable<Term> Parse(string query)
        {
            var termCount = 0;
            foreach (var term in query.Split(' '))
            {
                var segments = term.Split(':');
                var field = segments[0];
                var value = segments[1];

                var and = false;
                var not = false;
                var prefix = false;
                var fuzzy = false;

                if (0 == termCount++) and = true;

                if (field[0] == '+')
                {
                    field = new string(field.Skip(1).ToArray());
                    and = true;
                }
                else if (field[0] == '-')
                {
                    field = new string(field.Skip(1).ToArray());
                    not = true;
                }

                if (value[value.Length - 1] == '*')
                {
                    value = new string(value.Take(value.Length - 1).ToArray());
                    prefix = true;
                }
                else if (value[value.Length - 1] == '~')
                {
                    value = new string(value.Take(value.Length - 1).ToArray());
                    fuzzy = true;
                }

                foreach (var token in _analyzer.Analyze(value))
                {
                    yield return new Term {Field = field, Token = token, And = and, Not = not, Prefix = prefix, Fuzzy = fuzzy};
                }
            }
        }
    }
}