using System;

namespace Resin
{
    public class DocumentScore : IEquatable<DocumentScore>
    {
        private readonly string _docId;
        private readonly object _postingData;

        public string DocId { get { return _docId; } }
        public object PostingData { get { return _postingData; } }

        public double Score { get; set; }

        public DocumentScore(string docId, object postingData)
        {
            _docId = docId;
            _postingData = postingData;
        }

        public DocumentScore Add(DocumentScore score)
        {
            if (!score.Equals(this)) throw new ArgumentException("Doc id differs. Cannot add.", "score");

            Score += score.Score;
            return this;
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

        public override string ToString()
        {
            return string.Format("docid:{0} rawtf:{1} score:{2}",
                _docId, _postingData, Score);
        }
    }
}