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
            _stopwords =stopwords == null ? null : new HashSet<string>(stopwords);
        }

        public virtual AnalyzedDocument AnalyzeDocument(Document document)
        {
            var words = new Dictionary<Term, DocumentPosting>();

            foreach(var field in document.Fields)
            {
                if (field.Key[0] == '_')
                {
                    var term = new Term(field.Key, new Word(field.Value));
                    DocumentPosting posting;

                    if (words.TryGetValue(term, out posting))
                    {
                        posting.Count++;
                    }
                    else
                    {
                        words.Add(term, new DocumentPosting(document.Id, 1));
                    }
                }
                else
                {
                    var tokenDic = new Dictionary<string, int>();
                    foreach(var token in Analyze(field.Value))
                    {
                        if (tokenDic.ContainsKey(token))
                        {
                            tokenDic[token]++;
                        }
                        else
                        {
                            tokenDic[token] = 1;
                        }
                    }

                    foreach (var tokenGroup in tokenDic)
                    {
                        var word = new Word(tokenGroup.Key);
                        var term = new Term(field.Key, word);

                        DocumentPosting posting;

                        var count = tokenGroup.Value;

                        if (words.TryGetValue(term, out posting))
                        {
                            posting.Count += count;
                        }
                        else
                        {
                            posting = new DocumentPosting(document.Id, count);
                            words.Add(term, posting);
                        }
                    }
                }
            }
            return new AnalyzedDocument(document.Id, words);
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
            var text = new string(washed);
            var result = text.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

            if (_stopwords == null) return result;

            return result.Where(s => !_stopwords.Contains(s));
        }

        protected virtual bool IsNoice(char c)
        {
            if (_customTokenSeparators == null) return !char.IsLetterOrDigit(c);

            if (char.IsLetterOrDigit(c)) return _customTokenSeparators.Contains(c);

            return true;
        }
    }
}