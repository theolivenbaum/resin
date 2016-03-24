using System;

namespace Resin
{
    public class Tfidf
    {
        private readonly double _idf;

        public Tfidf(int totalNumberOfDocs, int hitCount)
        {
            _idf = Math.Log((double)totalNumberOfDocs / (1 + hitCount)) + 1;
        }

        public void Score(DocumentScore doc)
        {
            doc.Score += Math.Sqrt(doc.TermFrequency) * _idf;
        }
    }
}