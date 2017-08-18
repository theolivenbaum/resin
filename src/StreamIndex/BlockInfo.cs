using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace StreamIndex
{
    [DebuggerDisplay("{Position} {Length}")]
    public struct BlockInfo : IEquatable<BlockInfo>
    {
        public long Position;
        public int Length;

        public BlockInfo(long position, int length)
        {
            Position = position;
            Length = length;
        }

        public bool Equals(BlockInfo other)
        {
            return other.Position == Position && other.Length == Length;
        }

        public override bool Equals(object obj)
        {
            return Equals((BlockInfo)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Position.GetHashCode();
                hashCode = (hashCode * 397) ^ Length.GetHashCode();
                return hashCode;
            }
        }
    }

    public class BlockInfoPositionComparer : IComparer<BlockInfo>
    {
        public int Compare(BlockInfo x, BlockInfo y)
        {
            if (x.Position > y.Position) return 1;
            if (x.Position < y.Position) return -1;
            return 0;
        }
    }
}