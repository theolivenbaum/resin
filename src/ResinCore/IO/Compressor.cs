using System.IO;
using System.IO.Compression;

namespace Resin.IO
{
    public static class Deflator
    {
        public static byte[] Compress(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new byte[0];

            var bytes = Serializer.Encoding.GetBytes(text);

            return Compress(bytes);
        }

        public static byte[] Compress(byte[] data)
        {
            var output = new MemoryStream();

            using (var dstream = new DeflateStream(output, CompressionLevel.Optimal))
            {
                dstream.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }

        public static byte[] Deflate(byte[] data)
        {
            var input = new MemoryStream(data);
            var output = new MemoryStream();

            using (var dstream = new DeflateStream(input, CompressionMode.Decompress))
            {
                dstream.CopyTo(output);
            }

            return output.ToArray();
        }
    }
}