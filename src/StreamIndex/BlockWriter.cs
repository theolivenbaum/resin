using System;
using System.IO;

namespace StreamIndex
{
    public abstract class BlockWriter<T> : IDisposable
    {
        protected abstract int Serialize(T block, Stream stream);

        private readonly Stream _stream;

        public Stream Stream { get { return _stream; } }

        public BlockWriter(Stream stream)
        {
            _stream = stream;
        }

        public BlockInfo Write(T block)
        {
            var offset = _stream.Position;
            var size = Serialize(block, _stream);
            var info = new BlockInfo(offset, size);

            if (info.Length < 1)
                throw new InvalidOperationException(
                    string.Format("invalid length {0}", info.Length));

            return info;
        }

        public void Flush()
        {
            _stream.Flush();
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}