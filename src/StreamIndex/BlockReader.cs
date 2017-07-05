using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StreamIndex
{
    public abstract class BlockReader<T> : IDisposable
    {
        protected abstract T Deserialize(byte[] data);

        private readonly long _offset;

        private readonly Stream _stream;

        protected BlockReader(Stream stream):this(stream, 0)
        {
        }

        protected BlockReader(Stream stream, long offset)
        {
            _stream = stream;
            _offset = offset;
        }

        public IList<T> Read(IList<BlockInfo> blocks)
        {
            var result = new List<T>(blocks.Count);
            foreach (var block in blocks)
            {
                result.Add(ReadInternal(block));
            }
            return result;
        }

        private T ReadInternal(BlockInfo info)
        {
            if (info.Length < 1)
                throw new ArgumentOutOfRangeException(
                    "info", string.Format("invalid length {0}", info.Length));

            _stream.Seek(_offset + info.Position, SeekOrigin.Begin);

            byte[] buffer = new byte[info.Length];

            _stream.Read(buffer, 0, buffer.Length);

            return Deserialize(buffer);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}