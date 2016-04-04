using System;
using System.Text;

namespace Resin
{
    public class DocumentScore : IEquatable<DocumentScore>
    {
        private readonly StringBuilder _trace;
        private readonly string _docId;
        private readonly double _termFreq;

        public string DocId { get { return _docId; } }
        public double TermFrequency { get { return _termFreq; } }
        public double Score { get; set; }
        public StringBuilder Trace { get { return _trace; } }
        public Tfidf Scorer { get; set; }

        public DocumentScore(string docId, double termFreq)
        {
            _docId = docId;
            _termFreq = termFreq;
            _trace = new StringBuilder();
        }

        public DocumentScore Add(DocumentScore score)
        {
            if (!score.Equals(this)) throw new ArgumentException("Doc id differs. Cannot add.", "score");

            var scorer = new Tfidf((Scorer.Idf + score.Scorer.Idf)/2);
            var d = new DocumentScore(DocId, (TermFrequency + score.TermFrequency)/2);
            scorer.Score(d);
            return d;
        }

        public int CompareTo(DocumentScore other)
        {
            return String.Compare(other.DocId, DocId, StringComparison.Ordinal);
        }

        public bool Equals(DocumentScore other)
        {
            return other.DocId.Equals(DocId);
        }

        public int CompareTo(object obj)
        {
            return CompareTo((DocumentScore) obj);
        }

        public override int GetHashCode()
        {
            return DocId.GetHashCode();
        }
    }
}