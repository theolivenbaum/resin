using System;

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
        /// </summary>
        /// <param name="docsInCorpus"></param>
        /// <param name="docsWithTerm"></param>
        public TfIdf(int docsInCorpus, int docsWithTerm)
        {
            _idf = Math.Log10(docsInCorpus - docsWithTerm / (double)docsWithTerm);
        }

        public double Score(int termCount)
        {
            return 1 + Math.Log10(termCount) * _idf;
        }
    }
}