using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Sir.Store
{
    public static class StreamHelper
    {
        public static byte[] Concat(byte[] buf1, byte[] buf2)
        {
            byte[] result = new byte[buf1.Length + buf2.Length];
            Buffer.BlockCopy(buf1, 0, result, 0, buf1.Length);
            Buffer.BlockCopy(buf2, 0, result, buf1.Length, buf2.Length);
            return result;
        }

        public static byte[] Concat(byte[] buf1, byte[] buf2, byte[] buf3)
        {
            byte[] result = new byte[buf1.Length + buf2.Length + buf3.Length];
            Buffer.BlockCopy(buf1, 0, result, 0, buf1.Length);
            Buffer.BlockCopy(buf2, 0, result, buf1.Length, buf2.Length);
            Buffer.BlockCopy(buf3, 0, result, buf1.Length + buf2.Length, buf3.Length);
            return result;
        }

        public static byte[] Concat(byte[] buf1, byte[] buf2, byte[] buf3, byte[] buf4)
        {
            byte[] result = new byte[buf1.Length + buf2.Length + buf3.Length + buf4.Length];
            Buffer.BlockCopy(buf1, 0, result, 0, buf1.Length);
            Buffer.BlockCopy(buf2, 0, result, buf1.Length, buf2.Length);
            Buffer.BlockCopy(buf3, 0, result, buf1.Length + buf2.Length, buf3.Length);
            Buffer.BlockCopy(buf4, 0, result, buf1.Length + buf2.Length + buf3.Length, buf4.Length);
            return result;
        }

        public static byte[] ToStreamWithHeader(this IEnumerable<long> items, long header)
        {
            var payload = new MemoryStream();

            payload.Write(BitConverter.GetBytes(header));

            foreach (var item in items)
            {
                var buf = BitConverter.GetBytes(item);

                payload.Write(buf);
            }

            return payload.ToArray();
        }

        public static byte[] ToStreamWithHeader(this IEnumerable<BigInteger> items, long header)
        {
            var payload = new MemoryStream();

            payload.Write(BitConverter.GetBytes(header));

            foreach (var item in items)
            {
                var buf = item.ToByteArray();

                payload.Write(buf);
            }

            return payload.ToArray();
        }

        public static byte[] ToStream(this IEnumerable<long> items)
        {
            var payload = new MemoryStream();

            foreach (var item in items)
            {
                var buf = BitConverter.GetBytes(item);

                payload.Write(buf, 0, buf.Length);
            }

            return payload.ToArray();
        }

        public static byte[] ToStream(this IEnumerable<int> items)
        {
            var payload = new MemoryStream();

            foreach (var item in items)
            {
                var buf = BitConverter.GetBytes(item);

                payload.Write(buf, 0, buf.Length);
            }

            return payload.ToArray();
        }

        public static byte[] ToStream(this IEnumerable<KeyValuePair<long, int>> items)
        {
            var payload = new MemoryStream();

            foreach (var item in items)
            {
                payload.Write(BitConverter.GetBytes(item.Key));
                payload.Write(BitConverter.GetBytes(item.Value));
            }

            return payload.ToArray();
        }
    }
}
