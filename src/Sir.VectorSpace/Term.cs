using System.Collections.Generic;

namespace Sir.VectorSpace
{
    [System.Diagnostics.DebuggerDisplay("{Key}:{Label}")]
    public class Term : BooleanStatement
    {
        public IVector Vector { get; }
        public long KeyId { get; }
        public string Key { get; }
        public string Directory { get; }
        public ulong CollectionId { get; }
        public IList<long> PostingsOffsets { get; set; }
        public double Score { get; set; }
        public object Label => Vector.Label;
        public IList<(ulong collectionId, long documentId)> Result { get; set; }

        public Term(
            string directory,
            ulong collectionId,
            long keyId, 
            string key, 
            IVector vector, 
            bool and, 
            bool or, 
            bool not)
            : base(and, or, not)
        {
            Directory = directory;
            CollectionId = collectionId;
            KeyId = keyId;
            Key = key;
            Vector = vector;
            IsIntersection = and;
            IsUnion = or;
            IsSubtraction = not;
        }
    }
}