using DocumentTable;
using System.Collections.Generic;

namespace Resin.Analysis
{
    public class Analyzer : IAnalyzer
    {
        private readonly ITokenizer _tokenizer;
 
        public Analyzer(ITokenizer tokenizer = null, int[] tokenSeparators = null)
        {
            _tokenizer = tokenizer ?? new Tokenizer(tokenSeparators);
        }

        public AnalyzedDocument AnalyzeDocument(Document document)
        {
            var terms = AnalyzeDocumentInternal(document);
            return new AnalyzedDocument(document.Id, terms);
        }

        private IList<AnalyzedTerm> AnalyzeDocumentInternal(Document document)
        {
            var analyzedTerms = new List<AnalyzedTerm>();

            foreach(var field in document.Fields.Values)
            {
                if (field.Analyze && field.Index)
                {
                    var tokens = Analyze(field.Value);
                    var tokenDic = new Dictionary<string, int>();

                    foreach (var token in tokens)
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

                        analyzedTerms.Add(new AnalyzedTerm(term, posting));
                    }
                }
                else if (field.Index)
                {
                    var term = new Term(field.Key, new Word(field.Value));
                    var posting = new DocumentPosting(document.Id, 1);

                    analyzedTerms.Add(new AnalyzedTerm(term, posting));
                }
            }
            return analyzedTerms;
        }

        public IList<string> Analyze(string value)
        {
            var tokens = new List<(int Start, int Length)>();

            _tokenizer.Tokenize(value, tokens);

            var result = new List<string>();

            foreach (var token in tokens)
            {
                result.Add(value.Substring(token.Start, token.Length).ToLowerInvariant());
            }

            return result;
        }
    }
}