using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public class PostingsReader
    {
        private readonly Stream _stream;

        public PostingsReader(Stream stream)
        {
            _stream = stream;
        }

        public IEnumerable<ulong> Read(long offset, int len)
        {
            _stream.Seek(offset, SeekOrigin.Begin);

            var buf = new byte[len];
            var read = _stream.Read(buf, 0, len);

            if (read != len)
            {
                throw new InvalidDataException();
            }

            var position = 0;

            while (position < len)
            {
                yield return BitConverter.ToUInt64(buf, position * sizeof(ulong));
                position += sizeof(ulong);
            }
            
        }
    }
}
