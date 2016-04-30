using System.Collections.Generic;

namespace Resin
{
    public interface IScoringScheme
    {
        void Score(DocumentScore doc);
        void Eval(string field, string value, IAnalyzer analyzer, Dictionary<string, object> postingData);
        IScoringScheme CreateScorer(int totalNumOfDocs, int hitCount);
    }
}