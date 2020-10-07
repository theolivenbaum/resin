using System;
using System.IO;

namespace Sir.Mnist
{
    public static class BinaryHelper
    {
        public static int ReadInt32WithCorrectEndianness(this BinaryReader br)
        {
            var bytes = br.ReadBytes(sizeof(int));
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}
