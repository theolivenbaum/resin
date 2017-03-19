using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.IO.Read
{
    public class BlockReader<T> : IDisposable
    {
        private readonly Stream _stream;
        private long _position;

        public BlockReader(Stream stream)
        {
            _stream = stream;
            _position = 0;
        }

        public IEnumerable<T> Get(IEnumerable<BlockInfo> blocks)
        {
            return blocks.Select(Get);
        }

        private T Get(BlockInfo info)
        {
            var distance = info.Position - _position;

            if (distance > 0)
            {
                _stream.Seek(distance, SeekOrigin.Current);
            }

            var buffer = new byte[info.Length];

            _stream.Read(buffer, 0, buffer.Length);

            _position = info.Position + info.Length;

            return Deserialize(buffer);
        }

        private T Deserialize(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return (T)GraphSerializer.Serializer.Deserialize(stream);
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}