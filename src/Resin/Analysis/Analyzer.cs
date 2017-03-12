using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Resin.Sys;

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
            _stopwords = new HashSet<string>(stopwords ?? GetDefaultStopwords());
        }

        private string[] GetDefaultStopwords()
        {
            var dir = Path.Combine(Util.GetDataDirectory(), "stopwords");
            var fileName = Path.Combine(dir, _culture.Name + ".txt");
            if (File.Exists(fileName)) return File.ReadAllLines(fileName);
            return new string[0];
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
            if (value == null) yield break;

            int token = 0;
            var lowerStr = value.ToLower(_culture);

            for (int i = 0; i < lowerStr.Length; ++i)
            {
                if (!IsSeparator(lowerStr[i]))
                {
                    continue;
                }

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
            if (char.IsControl(c) || char.IsSeparator(c) || char.IsWhiteSpace(c)) return true;

            var cat = char.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.CurrencySymbol) return false;

            if (char.IsPunctuation(c))
            {
                var code = (int) c;
                return code != 39 && code != 96;
            }

            return char.IsPunctuation(c) || _customTokenSeparators.Contains(c);
        }
    }
}