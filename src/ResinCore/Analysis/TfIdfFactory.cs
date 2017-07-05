namespace Resin.Analysis
{
    public class TfIdfFactory : IScoringSchemeFactory
    {
        public IScoringScheme CreateScorer(int docsInCorpus, int docsWithTerm)
        {
            return new TfIdf(docsInCorpus, docsWithTerm);
        }
    }
}