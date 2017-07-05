using DocumentTable;

namespace Resin
{
    public interface IScoringScheme
    {
        double Score(DocumentPosting posting);
        IScoringScheme CreateScorer(int docsInCorpus, int docsWithTerm);
    }
}