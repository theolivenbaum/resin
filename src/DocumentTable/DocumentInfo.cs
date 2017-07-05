using System;

namespace DocumentTable
{
    public struct DocumentInfo : IEquatable<DocumentInfo>
    {
        public UInt64 Hash { get; private set; }
        public bool IsObsolete { get; set; }

        public DocumentInfo(UInt64 hash)
        {
            Hash = hash;
            IsObsolete = false;
        }

        public DocumentInfo(UInt64 hash, bool isObsolete)
        {
            Hash = hash;
            IsObsolete = isObsolete;
        }

        public bool Equals(DocumentInfo other)
        {
            return other.Hash.Equals(Hash);
        }
    }
}