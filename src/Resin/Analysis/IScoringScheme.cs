using Resin.IO;

namespace Resin.Analysis
{
    public interface IScoringScheme
    {
        double Score(DocumentPosting posting);
        IScoringScheme CreateScorer(int docsInCorpus, int docsWithTerm);
    }
}