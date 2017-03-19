using System;
using System.IO;

namespace Resin.IO.Write
{
    public class BlockWriter<T> : IDisposable
    {
        private long _position;
        private readonly Stream _stream;

        public BlockWriter(Stream stream)
        {
            _position = 0;
            _stream = stream;
        }

        public BlockInfo Write(T block)
        {
            var bytes = Serialize(block);
            var info = new BlockInfo(_position, bytes.Length);

            _stream.Write(bytes, 0, bytes.Length);
            _position += bytes.Length;

            return info;
        }

        private byte[] Serialize(T block)
        {
            using (var ms = new MemoryStream())
            {
                GraphSerializer.Serializer.Serialize(ms, block);
                return ms.ToArray();
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}