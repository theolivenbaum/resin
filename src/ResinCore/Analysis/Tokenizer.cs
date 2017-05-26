using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Resin.Analysis
{
    public class Tokenizer : ITokenizer
    {
        private readonly HashSet<char> _customTokenSeparators;
        private readonly HashSet<string> _stopwords;
        private static readonly Lazy<Tokenizer> _lazy=new Lazy<Tokenizer>(()=>new Tokenizer(),LazyThreadSafetyMode.PublicationOnly);
        public static  ITokenizer Instance {get { return _lazy.Value; } }

        public Tokenizer(char[] tokenSeparators = null, string[] stopwords = null)
        {
            _customTokenSeparators = tokenSeparators == null ? null : new HashSet<char>(tokenSeparators);
            _stopwords = stopwords == null ? null : new HashSet<string>(stopwords);
        }

        public IEnumerable<string> Tokenize(string value)
        {
            var normalized = value.ToLower();

            var washed = new char[normalized.Length];

            for (int index = 0; index < normalized.Length; index++)
            {
                var c = normalized[index];

                if (IsNoice(c))
                {
                    washed[index] = ' ';
                }
                else
                {
                    washed[index] = c;
                }
            }
            var text = new string(washed);
            var result = text.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (_stopwords == null) return result;

            return result.Where(s => !_stopwords.Contains(s));
        }
      
        private bool IsNoice(char c)
        {
            if (_customTokenSeparators == null) return !char.IsLetterOrDigit(c);

            if (char.IsLetterOrDigit(c)) return _customTokenSeparators.Contains(c);

            return true;
        }
    }
}