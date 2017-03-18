using Resin.IO;
using Resin.Querying;

namespace Resin.Analysis
{
    public interface IScoringScheme
    {
        DocumentScore Score(DocumentPosting posting);
        IScoringScheme CreateScorer(int docsInCorpus, int docsWithTerm);
    }
}