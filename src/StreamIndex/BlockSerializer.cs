using System;
using System.IO;

namespace StreamIndex
{
    public static class BlockSerializer
    {
        public static int SizeOfBlock()
        {
            return 1 * sizeof(long) + 1 * sizeof(int);
        }

        public static BlockInfo DeserializeBlock(Stream stream)
        {
            var posBytes = new byte[sizeof(long)];
            var lenBytes = new byte[sizeof(int)];

            stream.Read(posBytes, 0, sizeof(long));
            stream.Read(lenBytes, 0, sizeof(int));

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(posBytes);
                Array.Reverse(lenBytes);
            }

            return new BlockInfo(BitConverter.ToInt64(posBytes, 0), BitConverter.ToInt32(lenBytes, 0));
        }

        public static BlockInfo DeserializeBlock(byte[] bytes)
        {
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            var pos = BitConverter.ToInt64(bytes, 0);
            var len = BitConverter.ToInt32(bytes, sizeof(long));

            return new BlockInfo(pos, len);
        }

        public static bool Serialize(this BlockInfo? block, Stream stream)
        {
            if (block == null)
            {
                var pos = BitConverter.GetBytes(long.MinValue);
                var len = BitConverter.GetBytes(int.MinValue);

                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(pos);
                    Array.Reverse(len);
                }

                stream.Write(pos, 0, pos.Length);
                stream.Write(len, 0, len.Length);

                return false;
            }
            else
            {
                block.Value.Serialize(stream);

                return true;
            }
        }

        public static int Serialize(this BlockInfo block, Stream stream)
        {
            byte[] posBytes = BitConverter.GetBytes(block.Position);
            byte[] lenBytes = BitConverter.GetBytes(block.Length);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(posBytes);
                Array.Reverse(lenBytes);
            }

            stream.Write(posBytes, 0, posBytes.Length);
            stream.Write(lenBytes, 0, lenBytes.Length);

            return posBytes.Length + lenBytes.Length;
        }
    }
}
