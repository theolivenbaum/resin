using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin.IO.Read
{
    public abstract class AutoResettingBlockReader<T> : IDisposable
    {
        protected abstract T Deserialize(byte[] data);

        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private long _position;

        protected AutoResettingBlockReader(Stream stream, bool leaveOpen = false)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
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

            if (info.Position < 0)
            {
                _position = 0;

                distance = info.Position - _position;

                _stream.Seek(distance, SeekOrigin.Begin);
            }
            else
            {
                _stream.Seek(info.Position, SeekOrigin.Begin);
            }

            byte[] buffer = new byte[info.Length];

            _stream.Read(buffer, 0, buffer.Length);

            _position = info.Position + info.Length;

            return Deserialize(buffer);
        }

        public void Dispose()
        {
            if(!_leaveOpen) _stream.Dispose();
        }
    }
}