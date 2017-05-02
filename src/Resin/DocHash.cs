using System;

namespace Resin
{
    public class DocHash
    {
        public UInt32 Hash { get; private set; }
        public bool IsObsolete { get; set; }

        public DocHash(UInt32 hash)
        {
            Hash = hash;
        }

        public DocHash(UInt32 hash, bool isObsolete)
        {
            Hash = hash;
            IsObsolete = isObsolete;
        }
    }
}