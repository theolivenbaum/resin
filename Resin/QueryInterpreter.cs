using System.Linq;

namespace Resin
{
    public class QueryInterpreter
    {
        private readonly char[] _fieldOperators = { '+','-' };
        private readonly char[] _termOperators = { '~', '*' };

        private readonly IAnalyzer _analyzer;
        private readonly string _text;
        private DataType _state;
        private QueryContext _root;
        private QueryContext _cursor;
        private int _start;
        private int _lastIndexOfData;

        private string _field;
        private string _word;
        private bool _and;
        private bool _not;
        private bool _prefix;
        private bool _fuzzy;

        public QueryInterpreter(string text, IAnalyzer analyzer)
        {
            _text = text;
            _analyzer = analyzer;
            _and = true;
        }

        private int GetLength()
        {
            return _text.Length - _start - (_text.Length - _lastIndexOfData - 1);
        }

        public QueryContext GetQuery()
        {
            var length = GetLength();
            _word = _text.Substring(_start, length);
            if (!string.IsNullOrWhiteSpace(_field) && !string.IsNullOrWhiteSpace(_word))
            {
                foreach (var token in _analyzer.Analyze(_word))
                {
                    Add(new QueryContext(_field, token) { And = _and, Not = _not, Prefix = _prefix, Fuzzy = _fuzzy, Similarity = _fuzzy ? 0.75f : 0f });
                }
            }
            return _root;
        }

        private QueryContext Add(QueryContext term)
        {
            if (_root == null)
            {
                _root = term;
                _cursor = _root;
            }
            else
            {
                _cursor.Children.Add(term);
            }
            
            return _root;
        }

        public void Step(int index)
        {
            var c = _text[index];
            var type = GetCategory(c);

            if (type == Category.FieldOperator)
            {
                if (_state == DataType.Token || _state == DataType.TokenOperator)
                {
                    var length = GetLength();
                    _word = _text.Substring(_start, length);
                    foreach (var token in _analyzer.Analyze(_word))
                    {
                        _cursor = Add(new QueryContext(_field, token) { And = _and, Not = _not, Prefix = _prefix, Fuzzy = _fuzzy, Similarity = _fuzzy ? 0.75f : 0f });                       
                    }
                    _field = null;
                    _word = null;
                    _and = false;
                    _not = false;
                    _prefix = false;
                    _fuzzy = false;
                }
                if (c == '+') _and = true;
                else if (c == '-') _not = true;
                _start = index + 1;
                _state = DataType.FieldOperator;
            }
            else if (type == Category.EndOfField)
            {
                var length = GetLength();
                _field = _text.Substring(_start, length);
                _start = index + 1;
                _state = DataType.EndOfField;
            }
            else if (type == Category.TokenOperator)
            {
                if (c == '~') _fuzzy = true;
                else if (c == '*') _prefix = true;
                _lastIndexOfData = index - 1;
                _state = DataType.TokenOperator;
            }
            else if (type == Category.Data)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (_state == DataType.Token || _state == DataType.TokenOperator)
                    {
                        var length = GetLength();
                        _word = _text.Substring(_start, length);
                        foreach (var token in _analyzer.Analyze(_word))
                        {
                            _cursor = Add(new QueryContext(_field, token) { And = _and, Not = _not, Prefix = _prefix, Fuzzy = _fuzzy, Similarity = _fuzzy ? 0.75f : 0f });
                        }
                        _field = null;
                        _word = null;
                        _and = false;
                        _not = false;
                        _prefix = false;
                        _fuzzy = false;
                        _start = index + 1;
                    }
                    _state = DataType.FieldOperator;
                }
                else
                {
                    if (_state == DataType.FieldOperator)
                    {
                        _state = DataType.Field;
                    }
                    else if (_state == DataType.EndOfField)
                    {
                        _state = DataType.Token;
                    }
                    _lastIndexOfData = index;  
                }
            }
        }

        private Category GetCategory(char c)
        {
            if (_fieldOperators.Contains(c)) return Category.FieldOperator;
            if (_termOperators.Contains(c)) return Category.TokenOperator;
            if (c == ':') return Category.EndOfField;
            return Category.Data;
        }

        private enum Category
        {
            FieldOperator,
            TokenOperator,
            EndOfField,
            Data
        }

        private enum DataType
        {
            FieldOperator,
            TokenOperator,
            EndOfField,
            Token,
            Field
        }
    }
}