using System;
using System.Collections.Generic;
using System.Linq;
using Resin.Querying;

namespace Resin.IO
{
    [Serializable]
    public class DocumentPosting
    {
        private readonly string _documentId;
        private int _count;

        [NonSerialized]
        private string _field;

        [NonSerialized]
        private DocumentScore _score;

        public string DocumentId
        {
            get { return _documentId; }
        }

        public int Count
        {
            get { return _count; }
        }

        public string Field
        {
            get { return _field; }
            set { _field = value; }
        }

        public DocumentScore Score
        {
            get { return _score; }
            set { _score = value; }
        }

        public DocumentPosting(string documentId, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException("count");

            _documentId = documentId;
            _count = count;
        }

        public void Combine(DocumentPosting other)
        {
            if (other.DocumentId != DocumentId) throw new ArgumentException();

            _count += other.Count;
            Score.Combine(other.Score);
        }

        public static IEnumerable<DocumentPosting> JoinOr(IEnumerable<DocumentPosting> first, IEnumerable<DocumentPosting> second)
        {
            if (first == null) return second;

            return first.Concat(second).GroupBy(x => x.DocumentId).Select(group =>
            {
                var list = group.ToList();
                var top = list.First();
                foreach (var posting in list.Skip(1))
                {
                    top.Combine(posting);
                }
                return top;
            });
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", DocumentId, Count);
        }
    }
}