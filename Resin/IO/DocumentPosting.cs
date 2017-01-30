using System;

namespace Resin.IO
{
    [Serializable]
    public class DocumentPosting : IEquatable<DocumentPosting>
    {
        public string DocumentId { get; private set; }
        public int Count { get; private set; }

        public DocumentPosting(string documentId, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException("count");

            DocumentId = documentId;
            Count = count;
        }

        public bool Equals(DocumentPosting other)
        {
            if (other == null) return false;
            return other.DocumentId == DocumentId && other.Count == Count;
        }

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash*7) + DocumentId.GetHashCode();
            hash = (hash*7) + Count.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", DocumentId, Count);
        }
    }
}