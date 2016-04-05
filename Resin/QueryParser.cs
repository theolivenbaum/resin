using System.Collections.Generic;
using System.Linq;

namespace Resin
{
    public class QueryInterpreter
    {
        private Term _root;
        private readonly string _text;
        private int _start;


        private readonly char[] _fieldOperators = { '+','-' };
        private readonly char[] _termOperators = { '~', '*' };


        public Term Terms { get { return _root; } }

        private string _field;
        private string _token;
        private bool _and;
        private bool _not;
        private bool _prefix;
        private bool _fuzzy;

        public QueryInterpreter(string text)
        {
            _text = text;
        }

        public void Step(int index)
        {
            var c = _text[index];
            var type = Analyze(c);

            if (type == InputType.FieldOperator)
            {
                if (c == '+') _and = true;
                else if (c == '-') _not = true;
            }
            else if (type == InputType.TermOperator)
            {
                if (c == '~') _fuzzy = true;
                else if (c == '*') _prefix = true;
            }
            else if (type == InputType.EndOfField)
            {
                _field = _text.Substring(_start, index - _start);
            }
        }

        

        private InputType Analyze(char c)
        {
            if (_fieldOperators.Contains(c)) return InputType.FieldOperator;
            if (_termOperators.Contains(c)) return InputType.TermOperator;
            if (char.IsLetterOrDigit(c)) return InputType.Data;
            if(c==':') return InputType.EndOfField;
            return InputType.Other;
        }

        private enum InputType
        {
            TermOperator,
            FieldOperator, 
            EndOfField,
            Data, 
            Other
        }

        private enum State
        {
            Start,
            FieldOperator,
            Field,
            EndOfField,
            Token,
            TokenOperator,
        }
    }



    public class QueryParser
    {
        private readonly IAnalyzer _analyzer;

        public QueryParser(IAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public IEnumerable<Term> Parse(string query)
        {
            //var parser = new QueryInterpreter(query);
            //for (int i = 0; i < query.Length; i++)
            //{
            //    parser.Step(i);
            //}
            //var terms = parser.Terms;
















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

                if (field.StartsWith("_"))
                {
                    yield return new Term(field, value) { And = and, Not = not, Prefix = prefix, Fuzzy = fuzzy, Similarity = 0.75f };
                    yield break;
                }
                foreach (var token in _analyzer.Analyze(value))
                {
                    yield return new Term(field, token){And = and, Not = not, Prefix = prefix, Fuzzy = fuzzy, Similarity = 0.75f};
                }
            }
        }
    }
}