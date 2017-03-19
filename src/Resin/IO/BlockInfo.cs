namespace Resin.IO
{
    public struct BlockInfo
    {
        public long Position;
        public int Length;

        public BlockInfo(long position, int length)
        {
            Position = position;
            Length = length;
        }
    }
}