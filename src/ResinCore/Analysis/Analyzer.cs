using log4net;
using Resin.Documents;
using System.Collections.Generic;

namespace Resin.Analysis
{
    public class Analyzer : IAnalyzer
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(Analyzer));

        private readonly ITokenizer _tokenizer;
 
        public Analyzer(ITokenizer tokenizer = null, int[] tokenSeparators = null)
        {
            _tokenizer = tokenizer ?? new Tokenizer(tokenSeparators);
        }

        public IList<AnalyzedTerm> AnalyzeDocument(Document document)
        {
            var analyzedTerms = new List<AnalyzedTerm>();

            foreach (var field in document.Fields.Values)
            {
                if (field.Analyze && field.Index)
                {
                    var words = Analyze(field.Value);
                    var wordMatrix = new Dictionary<string, IList<int>>();

                    for (var index = 0;index<words.Count;index++)
                    {
                        var word = words[index];
                        IList<int> positions;
                        if (wordMatrix.TryGetValue(word, out positions))
                        {
                            positions.Add(index);
                        }
                        else
                        {
                            wordMatrix.Add(word, new List<int> { index });
                        }

                        //Log.DebugFormat("found term {0} at pos {1}", word, index);
                    }

                    foreach (var wordInfo in wordMatrix)
                    {
                        var postings = new List<int>(wordInfo.Value.Count);

                        foreach (var position in wordInfo.Value)
                        {
                            postings.Add(position);
                        }

                        analyzedTerms.Add(
                            new AnalyzedTerm(document.Id, field.Key, wordInfo.Key, postings));
                    }
                }
                else if (field.Index)
                {
                    var postings = new int[] { 0 };

                    analyzedTerms.Add(
                        new AnalyzedTerm(document.Id, field.Key, field.Value, postings));
                }
            }
            return analyzedTerms;
        }

        public IList<string> Analyze(string value)
        {
            var tokens = new List<string>();

            _tokenizer.Tokenize(value, tokens);

            return tokens;
        }
    }
}