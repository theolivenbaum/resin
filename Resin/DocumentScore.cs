using System;

namespace Resin
{
    public class DocumentScore : IEquatable<DocumentScore>
    {
        public string DocId { get; set; }
        public float TermFrequency { get; set; }
        public double Score { get; set; }

        public int CompareTo(DocumentScore other)
        {
            return other.DocId.CompareTo(DocId);
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