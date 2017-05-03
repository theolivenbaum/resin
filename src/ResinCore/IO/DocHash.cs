using System;

namespace Resin.IO
{
    public class DocHash : IEquatable<DocHash>
    {
        public UInt64 Hash { get; private set; }
        public bool IsObsolete { get; set; }

        public DocHash(UInt64 hash)
        {
            Hash = hash;
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