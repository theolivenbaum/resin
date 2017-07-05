using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StreamIndex
{
    public abstract class BlockReader<T> : IDisposable
    {
        protected abstract T Deserialize(byte[] data);

        private readonly Stream _stream;
        private long _position;

        protected BlockReader(Stream stream)
        {
            _stream = stream;
            _position = 0;
        }

        public IEnumerable<T> Read(IList<BlockInfo> blocks)
        {
            return blocks.Select(ReadInternal);
        }

        private T ReadInternal(BlockInfo info)
        {
            if (info.Length < 1)
                throw new ArgumentOutOfRangeException(
                    "info", string.Format("invalid length {0}", info.Length));

            var distance = info.Position - _position;

            if (distance > 0)
            {
                _stream.Seek(distance, SeekOrigin.Current);
            }

            byte[] buffer = new byte[info.Length];

            _stream.Read(buffer, 0, buffer.Length);

            _position = info.Position + info.Length;

            return Deserialize(buffer);
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}