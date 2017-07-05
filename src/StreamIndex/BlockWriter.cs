using System;
using System.IO;

namespace StreamIndex
{
    public abstract class BlockWriter<T> : IDisposable
    {
        protected abstract byte[] Serialize(T block);

        private long _position;
        private readonly Stream _stream;

        public BlockWriter(Stream stream)
        {
            _position = stream.Position;
            _stream = stream;
        }

        public BlockInfo Write(T block)
        {
            var bytes = Serialize(block);

            var info = new BlockInfo(_position, bytes.Length);

            if (info.Length < 1)
                throw new InvalidOperationException(
                    string.Format("invalid length {0}", info.Length));

            _stream.Write(bytes, 0, bytes.Length);
            _position += bytes.Length;

            return info;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}