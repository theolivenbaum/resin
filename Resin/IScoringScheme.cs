using System.Collections.Generic;

namespace Resin
{
    public interface IScoringScheme
    {
        void Score(DocumentScore doc);
        void Analyze(string field, string value, IAnalyzer analyzer, Dictionary<string, int> termCount);
        IScoringScheme CreateScorer(int totalNumOfDocs, int hitCount);
    }
}