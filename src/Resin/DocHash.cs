using System;

namespace Resin
{
    public class DocHash
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
    }
}