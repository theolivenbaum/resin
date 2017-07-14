using System;
using DocumentTable;

namespace Resin.Analysis
{
    public class TfIdf : IScoringScheme
    {
        private readonly double _idf;

        public double Idf
        {
            get { return _idf; }
        }

        /// <summary>
        /// Create scorer. 
        /// On tf-idf: 
        /// https://en.wikipedia.org/wiki/Tf%E2%80%93idf
        /// </summary>
        /// <param name="docsInCorpus"></param>
        /// <param name="docsWithTerm"></param>
        public TfIdf(int docsInCorpus, int docsWithTerm)
        {
            // probabilistic inverse document frequency
            _idf = Math.Log10(docsInCorpus - docsWithTerm / (double)docsWithTerm);
        }

        public double Score(DocumentPosting posting)
        {
            // log-normalized term frequency
            return 1 + Math.Log10(posting.Count) * _idf;
        }
    }
}