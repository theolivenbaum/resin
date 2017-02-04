using Resin.Querying;

namespace Resin.Analysis
{
    public interface IScoringScheme
    {
        void Score(DocumentScore doc);
        IScoringScheme CreateScorer(int docsInCorpus, int docsWithTerm);
    }
}