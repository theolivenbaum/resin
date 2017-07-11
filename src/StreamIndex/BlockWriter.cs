using System;
using System.IO;

namespace StreamIndex
{
    public abstract class BlockWriter<T> : IDisposable
    {
        protected abstract byte[] Serialize(T block);

        private readonly Stream _stream;

        public Stream Stream { get { return _stream; } }

        public BlockWriter(Stream stream)
        {
            _stream = stream;
        }

        public BlockInfo Write(T block)
        {
            var pos = _stream.Position;

            var bytes = Serialize(block);

            var info = new BlockInfo(pos, bytes.Length);

            if (info.Length < 1)
                throw new InvalidOperationException(
                    string.Format("invalid length {0}", info.Length));

            _stream.Write(bytes, 0, bytes.Length);

            return info;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}