using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Resin.Analysis
{
    public class Analyzer : IAnalyzer
    {
        private readonly HashSet<char> _customTokenSeparators;
        private readonly HashSet<string> _stopwords;
        private readonly CultureInfo _culture;
 
        public Analyzer(CultureInfo culture = null, char[] tokenSeparators = null, string[] stopwords = null)
        {
            _culture = culture ?? Thread.CurrentThread.CurrentUICulture;
            _customTokenSeparators = new HashSet<char>(tokenSeparators ?? new char[0]);
            _stopwords = new HashSet<string>(stopwords ?? new string[0]);
        }

        public AnalyzedDocument AnalyzeDocument(Document document)
        {
            var id = document.Id;
            var analyzed = document.Fields.ToDictionary(field => field.Key, field => Analyze(field.Key, field.Value));
            return new AnalyzedDocument(id, analyzed);
        }

        private IDictionary<string, int> Analyze(string field, string value)
        {
            var termCount = new Dictionary<string, int>();
            Analyze(field, value, termCount);
            return termCount;
        }

        private void Analyze(string field, string value, Dictionary<string, int> termCount)
        {
            var analyze = field[0] != '_';
            var tokens = analyze ? Analyze(value) : new[] { value };

            foreach (var token in tokens)
            {
                if (termCount.ContainsKey(token)) termCount[token] = termCount[token] + 1;
                else termCount.Add(token, 1);
            }
        }

        public IEnumerable<string> Analyze(string value)
        {
            var normalized = value.ToLower(_culture);

            var washed = new char[normalized.Length];

            for (int index = 0; index < normalized.Length; index++)
            {
                var c = normalized[index];

                if (char.IsLetterOrDigit(c) && !_customTokenSeparators.Contains(c))
                {
                    washed[index] = c;
                }
                else
                {
                    washed[index] = ' ';
                }
            }

            return new string(washed).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Except(_stopwords);
        }

        private bool IsSeparator(char c)
        {
            if (char.IsControl(c) || char.IsSeparator(c) || char.IsWhiteSpace(c)) return true;

            if (char.IsPunctuation(c))
            {
                var code = (int) c;
                var isExempted = code == 39 || code == 96;
                return !isExempted;
            }

            return char.IsPunctuation(c) || _customTokenSeparators.Contains(c);
        }
    }
}