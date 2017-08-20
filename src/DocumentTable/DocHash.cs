using System;

namespace Resin.Documents
{
    // Note, this object's equatability property is key. Making this into a struct would only led to boxing.
    public class DocHash : IEquatable<DocHash>
    {
        public UInt64 Hash { get; private set; }
        public bool IsObsolete { get; set; }

        public DocHash(UInt64 hash)
        {
            Hash = hash;
            IsObsolete = false;
        }

        public DocHash(UInt64 hash, bool isObsolete)
        {
            Hash = hash;
            IsObsolete = isObsolete;
        }

        public bool Equals(DocHash other)
        {
            return other.Hash.Equals(Hash);
        }
    }
}