using System;
using System.Collections.Generic;
using System.Linq;
using Resin.Querying;

namespace Resin.IO
{
    [Serializable]
    public class DocumentPosting
    {
        private readonly int _documentId;
        private int _count;

        [NonSerialized]
        private string _field;

        [NonSerialized]
        private DocumentScore _scoring;

        [NonSerialized]
        private string _indexName;

        public UInt32 Term { get; set; }

        public int DocumentId
        {
            get { return _documentId; }
        }

        public int Count
        {
            get { return _count; }
        }

        public string IndexName
        {
            get { return _indexName; }
            set { _indexName = value; }
        }

        public string Field
        {
            get { return _field; }
            set { _field = value; }
        }

        public DocumentScore Scoring
        {
            get { return _scoring; }
            set { _scoring = value; }
        }

        public DocumentPosting(int documentId, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException("count");

            _documentId = documentId;
            _count = count;
        }

        public void Combine(DocumentPosting other)
        {
            if (other.DocumentId != DocumentId) throw new ArgumentException();

            _count += other.Count;
            _indexName = other.IndexName;
            Scoring.Combine(other.Scoring);
        }

        public static IEnumerable<DocumentPosting> JoinOr(IEnumerable<DocumentPosting> first, IEnumerable<DocumentPosting> other)
        {
            if (first == null) return other;

            return first.Concat(other).GroupBy(x => x.DocumentId).Select(group =>
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

        public static IEnumerable<DocumentPosting> JoinAnd(IEnumerable<DocumentPosting> first, IEnumerable<DocumentPosting> other)
        {
            if (first == null) return other;

            var dic = other.ToDictionary(x => x.DocumentId);
            var remainder = new List<DocumentPosting>();

            foreach (var posting in first)
            {
                DocumentPosting exists;
                if (dic.TryGetValue(posting.DocumentId, out exists))
                {
                    posting.Combine(exists);
                    remainder.Add(posting);
                }
            }
            return remainder;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", DocumentId, Count);
        }
    }
}