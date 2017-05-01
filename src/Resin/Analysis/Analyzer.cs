using Resin.IO;
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
            _customTokenSeparators = tokenSeparators == null ? null : new HashSet<char>(tokenSeparators);
            _stopwords = new HashSet<string>(stopwords ?? new string[0]);
        }

        public virtual AnalyzedDocument AnalyzeDocument(Document document)
        {
            var fields = new Dictionary<string, LcrsTrie>();

            foreach(var field in document.Fields)
            {
                var trie = new LcrsTrie();
                fields.Add(field.Key, trie);

                if (field.Key.StartsWith("_"))
                {
                    trie.Add(field.Value, new DocumentPosting(document.Id, 1));
                }
                else
                {
                    foreach (var tokenGroup in Analyze(field.Value).GroupBy(token=>token))
                    {
                        trie.Add(tokenGroup.Key, new DocumentPosting(document.Id, tokenGroup.Count()));
                    }
                }
            }
            return new AnalyzedDocument(document.Id, fields);
        }
        
        public virtual IEnumerable<string> Analyze(string value)
        {
            var normalized = value.ToLower(_culture);

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

            return new string(washed).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Except(_stopwords);
        }

        protected virtual bool IsNoice(char c)
        {
            if (_customTokenSeparators == null) return !char.IsLetterOrDigit(c);

            if (char.IsLetterOrDigit(c)) return _customTokenSeparators.Contains(c);

            return true;
        }
    }
}