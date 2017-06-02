using Resin.IO;
using System.Collections.Generic;
using System.Linq;

namespace Resin.Analysis
{
    public class Analyzer : IAnalyzer
    {
        private readonly ITokenizer _tokenizer;
        private readonly HashSet<char> _customTokenSeparators;
        private readonly HashSet<string> _stopwords;
 
        public Analyzer(ITokenizer tokenizer = null, char[] tokenSeparators = null, string[] stopwords = null)
        {
            _customTokenSeparators = tokenSeparators == null ? null : new HashSet<char>(tokenSeparators);
            _stopwords = stopwords == null ? null : new HashSet<string>(stopwords);
            _tokenizer = tokenizer == null ? new Tokenizer(tokenSeparators, stopwords) : tokenizer;
        }

        public AnalyzedDocument AnalyzeDocument(Document document)
        {
            var terms = AnalyzeDocumentInternal(document);
            return new AnalyzedDocument(document.Id, terms);
        }

        protected virtual IEnumerable<AnalyzedTerm> AnalyzeDocumentInternal(Document document)
        {
            foreach(var field in document.Fields.Values)
            {
                if (field.Analyze)
                {
                    var tokenDic = new Dictionary<string, int>();
                    foreach (var token in Analyze(field.Value))
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
                        var posting = new DocumentPosting(document.Id, tokenGroup.Value);

                        yield return new AnalyzedTerm(term, posting);
                    }
                }
                else
                {
                    var term = new Term(field.Key, new Word(field.Value));
                    var posting = new DocumentPosting(document.Id, 1);

                    yield return new AnalyzedTerm(term, posting);
                }
            }
        }
        
        public virtual IEnumerable<string> Analyze(string value)
        {
            return _tokenizer.Tokenize(value);
        }

        private bool IsNoice(char c)
        {
            if (_customTokenSeparators == null) return !char.IsLetterOrDigit(c);

            if (char.IsLetterOrDigit(c)) return _customTokenSeparators.Contains(c);

            return true;
        }
    }
}