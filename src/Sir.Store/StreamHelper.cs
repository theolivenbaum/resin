using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public static class StreamHelper
    {
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
