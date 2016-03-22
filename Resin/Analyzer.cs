using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Resin
{
    public class Analyzer : IAnalyzer
    {
        private readonly char[] _tokenSeparators;
        private readonly HashSet<string> _stopwords;
        private readonly CultureInfo _culture;

        public Analyzer(CultureInfo culture = null, char[] tokenSeparators = null, string[] stopwords = null)
        {
            _culture = culture ?? Thread.CurrentThread.CurrentUICulture;
            _tokenSeparators = tokenSeparators ?? new char[0];
            _stopwords = new HashSet<string>(stopwords ?? new string[0]);
        }

        public IEnumerable<string> Analyze(string value)
        {
            var token = new List<char>();
            foreach (var c in value.ToLower(_culture))
            {
                if (IsSeparator(c))
                {
                    if (token.Count > 0)
                    {
                        var tok = new string(token.ToArray());
                        if (!_stopwords.Contains(tok)) yield return tok;
                        token.Clear();
                    }
                }
                else
                {
                    token.Add(c);
                }
            }
            if (token.Count > 0)
            {
                var tok = new string(token.ToArray());
                yield return tok;
            }
        }

        private bool IsSeparator(char c)
        {
            return
                char.IsControl(c) ||
                char.IsPunctuation(c) ||
                char.IsSeparator(c) ||
                char.IsWhiteSpace(c) ||
                _tokenSeparators.Contains(c);
        }
    }
}