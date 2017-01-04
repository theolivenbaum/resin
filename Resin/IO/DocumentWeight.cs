using System;

namespace Resin.IO
{
    [Serializable]
    public class DocumentWeight : IEquatable<DocumentWeight>
    {
        public string DocumentId { get; private set; }
        public int Weight { get; private set; }

        public DocumentWeight(string documentId, int weight)
        {
            DocumentId = documentId;
            Weight = weight;
        }

        public bool Equals(DocumentWeight other)
        {
            if (other == null) return false;
            return other.DocumentId == DocumentId && other.Weight == Weight;
        }

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash*7) + DocumentId.GetHashCode();
            hash = (hash*7) + Weight.GetHashCode();
            return hash;
        }
    }
}