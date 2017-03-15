using System;

namespace Resin.Querying
{
    public class DocumentScore
    {
        private readonly int _docId;
        
        private double _termCount;

        public int DocId { get { return _docId; } }
        public double TermCount { get { return _termCount; } }

        public double Score { get; set; }

        public DocumentScore(int docId, double termCount)
        {
            _docId = docId;
            _termCount = termCount;
        }

        public void Join(DocumentScore score)
        {
            if (!score.DocId.Equals(DocId)) throw new ArgumentException("Doc id differs. Cannot add.", "score");

            Score += score.Score;
            _termCount += score.TermCount;
        }

        public override string ToString()
        {
            return string.Format("docid:{0} rawtf:{1} score:{2}",
                _docId, _termCount, Score);
        }
    }
}