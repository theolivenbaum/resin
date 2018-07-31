using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public class DocReader
    {
        private readonly Stream _stream;

        public DocReader(Stream stream)
        {
            _stream = stream;
        }

        public IList<(uint keyId, uint valId)> Read(long offset, int length)
        {
            _stream.Seek(offset, SeekOrigin.Begin);

            var buf = new byte[length];
            var read = _stream.Read(buf, 0, length);

            if (read != length)
            {
                throw new InvalidDataException();
            }

            const int blockSize = sizeof(uint) + sizeof(uint);
            var blockCount = length / blockSize;
            var docMapping = new List<(uint, uint)>();

            for (int i = 0; i < blockCount; i++)
            {
                var offs = i * blockSize;
                var key = BitConverter.ToUInt32(buf, offs);
                var val = BitConverter.ToUInt32(buf, offs + sizeof(uint));

                docMapping.Add((key, val));
            }

            return docMapping;
        }
    }
}
