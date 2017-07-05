namespace Resin
{
    public interface IScoringSchemeFactory
    {
        IScoringScheme CreateScorer(int docsInCorpus, int docsWithTerm);
    }
}