using System;

namespace Resin.Querying
{
    public class DocumentScore : IEquatable<DocumentScore>, IComparable<DocumentScore>
    {
        private readonly string _docId;
        
        private double _termCount;

        public string DocId { get { return _docId; } }
        public double TermCount { get { return _termCount; } }

        public double Score { get; set; }

        public DocumentScore(string docId, double termCount)
        {
            _docId = docId;
            _termCount = termCount;
        }

        public void Combine(DocumentScore score)
        {
            if (!score.Equals(this)) throw new ArgumentException("Doc id differs. Cannot add.", "score");

            Score += score.Score;
            _termCount += score.TermCount;
        }

        public int CompareTo(DocumentScore other)
        {
            return String.Compare(other.DocId, DocId, StringComparison.Ordinal);
        }

        public bool Equals(DocumentScore other)
        {
            return other != null && other.DocId.Equals(DocId);
        }

        public int CompareTo(object obj)
        {
            return CompareTo((DocumentScore) obj);
        }

        public override int GetHashCode()
        {
            return DocId.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("docid:{0} rawtf:{1} score:{2}",
                _docId, _termCount, Score);
        }
    }
}