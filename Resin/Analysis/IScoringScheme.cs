using System.Collections.Generic;
using Resin.Querying;

namespace Resin.Analysis
{
    public interface IScoringScheme
    {
        void Score(DocumentScore doc);
        void Analyze(string field, string value, IAnalyzer analyzer, Dictionary<string, int> termCount);
        IScoringScheme CreateScorer(int docsInCorpus, int docsWithTerm);
    }
}