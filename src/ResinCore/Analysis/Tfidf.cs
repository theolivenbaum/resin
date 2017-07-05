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
        /// https://lucene.apache.org/core/6_4_1/core/org/apache/lucene/search/similarities/TFIDFSimilarity.html 
        /// https://en.wikipedia.org/wiki/Tf%E2%80%93idf
        /// </summary>
        /// <param name="docsInCorpus"></param>
        /// <param name="docsWithTerm"></param>
        public TfIdf(int docsInCorpus, int docsWithTerm)
        {
            _idf = Math.Log10(docsInCorpus / (double)docsWithTerm);
        }

        public double Score(DocumentPosting posting)
        {
            return Math.Sqrt(posting.Count) * _idf;
        }
    }
}