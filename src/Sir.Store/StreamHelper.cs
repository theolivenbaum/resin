using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.Store
{
    public static class StreamHelper
    {
        public static void SerializeHeaderAndPayload(IEnumerable<long> items, int itemCount, Stream stream)
        {
            Span<long> payload = stackalloc long[itemCount+1];

            payload[0] = itemCount;

            var index = 1;

            foreach (var item in items)
            {
                payload[index++] = item;
            }

            stream.Write(MemoryMarshal.Cast<long, byte>(payload));
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
