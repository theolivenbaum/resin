using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Resin
{
    public class Analyzer : IAnalyzer
    {
        private readonly HashSet<char> _customTokenSeparators;
        private readonly HashSet<string> _stopwords;
        private readonly CultureInfo _culture;
        private readonly IScoringScheme _scoringScheme;

        public Analyzer(IScoringScheme scoringScheme = null, CultureInfo culture = null, char[] tokenSeparators = null, string[] stopwords = null)
        {
            _scoringScheme = scoringScheme ?? new Tfidf();
            _culture = culture ?? Thread.CurrentThread.CurrentUICulture;
            _customTokenSeparators = new HashSet<char>(tokenSeparators ?? new char[0]);
            _stopwords = new HashSet<string>(stopwords ?? GetDefaultStopwords());
        }

        private string[] GetDefaultStopwords()
        {
            var dir = Path.Combine(Helper.GetDataDirectory(), "stopwords");
            var fileName = Path.Combine(dir, _culture.Name + ".txt");
            if (File.Exists(fileName)) return File.ReadAllLines(fileName);
            return new string[0];
        }

        public AnalyzedDocument AnalyzeDocument(IDictionary<string, string> document)
        {
            var id = document["_id"];
            var analyzed = document.ToDictionary(field => field.Key, field => Analyze(field.Key, field.Value));
            return new AnalyzedDocument(id, analyzed);
        }

        private IDictionary<string, object> Analyze(string field, string value)
        {
            var termCount = new Dictionary<string, object>();
            _scoringScheme.Analyze(field, value, this, termCount);
            return termCount;
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
            if (char.IsControl(c) || char.IsSeparator(c) || char.IsWhiteSpace(c)) return true;
            var cat = char.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.CurrencySymbol) return false;
            return char.IsPunctuation(c) || _customTokenSeparators.Contains(c);
        }
    }
}