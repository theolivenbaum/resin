using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Resin
{
    public class Analyzer : IAnalyzer
    {
        private readonly HashSet<char> _tokenSeparators;
        private readonly HashSet<string> _stopwords;
        private readonly CultureInfo _culture;

        public Analyzer(CultureInfo culture = null, char[] tokenSeparators = null, string[] stopwords = null)
        {
            _culture = culture ?? Thread.CurrentThread.CurrentUICulture;
            _tokenSeparators = new HashSet<char>(tokenSeparators ?? new char[0]);
            _stopwords = new HashSet<string>(stopwords ?? new string[0]);
        }

        public IEnumerable<string> Analyze(string value)
        {
            if (value == null) yield break;
            int token = 0;
            var lowerStr = value.ToLower(_culture);
            for (int i = 0; i < lowerStr.Length; ++i)
            {
                if (!IsSeparator(lowerStr[i])) continue;
                if (token < i)
                {
                    var tok = lowerStr.Substring(token, i - token);
                    if (!_stopwords.Contains(tok)) yield return tok;
                }
                token = i + 1;
            }
            if (token < lowerStr.Length)
            {
                yield return lowerStr.Substring(token);
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