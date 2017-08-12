using System;
using System.Collections.Generic;
using System.IO;

namespace StreamIndex
{
    public abstract class BlockReader<T> : IDisposable
    {
        protected abstract T Deserialize(long offset, int size, Stream stream);
        protected abstract T Clone(T input);
        private readonly long _offset;

        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly IDictionary<long, T> _cache = new Dictionary<long, T>();

        protected BlockReader(Stream stream, bool leaveOpen = false):this(stream, 0, leaveOpen)
        {
        }

        protected BlockReader(Stream stream, long offset, bool leaveOpen = false)
        {
            _stream = stream;
            _offset = offset;
            _leaveOpen = leaveOpen;
        }

        public T Read(BlockInfo block)
        {
            T cached;

            if (!_cache.TryGetValue(block.Position, out cached))
            {
                var read = ReadInternal(block);
                _cache.Add(block.Position, read);
                return Clone(read);
            }
            else
            {
                return Clone(cached);
            }
        }

        public IList<T> Read(IList<BlockInfo> blocks)
        {
            var result = new List<T>(blocks.Count);
            foreach (var block in blocks)
            {
                T cached;
                if (!_cache.TryGetValue(block.Position, out cached))
                {
                    var read = ReadInternal(block);
                    result.Add(read);
                    _cache.Add(block.Position, read);
                }
                else
                {
                    result.Add(Clone(cached));
                }

            }
            return result;
        }

        private T ReadInternal(BlockInfo info)
        {
            if (info.Length < 1)
                throw new ArgumentOutOfRangeException(
                    "info", string.Format("invalid length {0}", info.Length));

            return Deserialize(_offset + info.Position, info.Length, _stream);
        }

        public void Dispose()
        {
            if (!_leaveOpen) _stream.Dispose();
        }
    }
}