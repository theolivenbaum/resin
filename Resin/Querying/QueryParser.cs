using System;
using System.Collections.Generic;
using System.Linq;
using Resin.Analysis;

namespace Resin.Querying
{
    public class QueryParser
    {
        private readonly IAnalyzer _analyzer;

        public QueryParser(IAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public QueryContext Parse(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("query");

            QueryContext qc = null;
            var state = new List<char>();
            string field = null;
            var words = new List<string>();
            var termCount = 0;

            foreach (var c in query.Trim())
            {
                if (c == ':')
                {
                    var fieldName = new string(state.ToArray());
                    state = new List<char>();
                    if (field == null)
                    {
                        field = fieldName;
                    }
                    else
                    {
                        if (qc == null)
                        {
                            qc = CreateTerm(field, words, termCount++);
                        }
                        else
                        {
                            qc.Children.Add(CreateTerm(field, words, 1));
                        }
                        words = new List<string>();
                        field = fieldName;
                    }
                }
                else if (c == ' ')
                {
                    words.Add(new string(state.ToArray()));
                    state = new List<char>();
                }
                else
                {
                    state.Add(c);
                }
            }
            var word = new string(state.ToArray());
            if (!string.IsNullOrEmpty(word))
            {
                words.Add(word);
                if (qc == null)
                {
                    qc = CreateTerm(field, words, 0);
                }
                else
                {
                    qc.Children.Add(CreateTerm(field, words, 1));
                }
            }
            return qc;
        }

        private QueryContext CreateTerm(string field, IList<string> words, int termPositionInQuery)
        {
            var analyze = field[0] != '_';
            QueryContext qc = null;
            var defaulTokenOperator = words.Last().Last();

            foreach (var word in words)
            {
                if (analyze)
                {
                    var tokenOperator = word.Last();
                    var analyzable = word;
                    if (tokenOperator == '~' || tokenOperator == '*')
                    {
                        analyzable = word.Substring(0, word.Length - 1);
                    }
                    else
                    {
                        tokenOperator = defaulTokenOperator;
                    }
                    var analyzed = _analyzer.Analyze(analyzable).ToArray();
                    foreach (string token in analyzed)
                    {
                        if (qc == null)
                        {
                            qc = Parse(field, token, tokenOperator, termPositionInQuery);
                        }
                        else
                        {
                            var q = Parse(field, token, tokenOperator, termPositionInQuery);
                            q.And = false;
                            q.Not = false;
                            qc.Children.Add(q);
                        }
                    }
                }
                else
                {
                    if (qc == null)
                    {
                        qc = Parse(field, word);
                    }
                    else
                    {
                        var q = Parse(field, word);
                        q.And = false;
                        q.Not = false;
                        qc.Children.Add(q);
                    }
                }
            }
            return qc;
        }

        private QueryContext Parse(string field, string token, char tokenOperator = '\0', int position = 0)
        {
            var and = false;
            var not = false;
            var prefix = tokenOperator == '*';
            var fuzzy = tokenOperator == '~';

            string fieldName;
            if (field[0] == '-')
            {
                not = true;
                fieldName = field.Substring(1);
            }
            else if (field[0] == '+')
            {
                and = true;
                fieldName = field.Substring(1);
            }
            else
            {
                fieldName = field;
            }
            if (position == 0) and = true;
            return new QueryContext(fieldName, token) { And = and, Not = not, Prefix = prefix, Fuzzy = fuzzy, Similarity = 0.9f };
        }
    }
}