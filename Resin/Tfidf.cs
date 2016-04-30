using System;

namespace Resin
{
    public class Tfidf : IScorer
    {
        private readonly double _idf;

        public double Idf
        {
            get { return _idf; }
        }

        public Tfidf(int totalNumOfDocs, int hitCount)
        {
            _idf = Math.Log10(totalNumOfDocs / (double)hitCount);
        }

        public Tfidf(double idf)
        {
            _idf = idf;
        }

        public void Score(DocumentScore doc)
        {
            var tf = Math.Sqrt(doc.TermFrequency);
            doc.Score = tf * _idf;
        }
    }

    public interface IScorer
    {
        void Score(DocumentScore doc);
    }
}