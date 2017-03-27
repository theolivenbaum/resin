using System;
using Resin.IO;
using Resin.Querying;

namespace Resin.Analysis
{
    public class Tfidf : IScoringScheme
    {
        private readonly double _idf;

        public double Idf
        {
            get { return _idf; }
        }

        /// <summary>
        /// Create scoring scheme
        /// </summary>
        public Tfidf()
        {
        }

        /// <summary>
        /// Create scorer. 
        /// On tf-idf: 
        /// https://lucene.apache.org/core/6_4_1/core/org/apache/lucene/search/similarities/TFIDFSimilarity.html 
        /// https://en.wikipedia.org/wiki/Tf%E2%80%93idf
        /// </summary>
        /// <param name="docsInCorpus"></param>
        /// <param name="docsWithTerm"></param>
        public Tfidf(int docsInCorpus, int docsWithTerm)
        {
            _idf = Math.Log10(docsInCorpus / (double)docsWithTerm);
        }

        public DocumentScore Score(DocumentPosting posting)
        {
            var score = Math.Log10(posting.Count) * _idf;
            return new DocumentScore(posting.DocumentId, score);
        }

        public IScoringScheme CreateScorer(int docsInCorpus, int docsWithTerm)
        {
            return new Tfidf(docsInCorpus, docsWithTerm);
        }
    }
}