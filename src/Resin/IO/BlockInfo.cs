using System;
using System.Diagnostics;

namespace Resin.IO
{
    [Serializable, DebuggerDisplay("{Position} {Length}")]
    public struct BlockInfo
    {
        public long Position;
        public int Length;

        public BlockInfo(long position, int length)
        {
            Position = position;
            Length = length;
        }
        public static BlockInfo MinValue { get { return new BlockInfo(0, 0);} }
    }
}