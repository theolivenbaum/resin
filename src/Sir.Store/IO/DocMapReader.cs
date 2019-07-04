using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Document map stream reader, for fetching a document maps (key_id/val_id) from the document map stream.
    /// A document map is needed to re-contruct a complete document.
    /// </summary>
    public class DocMapReader : IDisposable
    {
        private readonly Stream _stream;

        public DocMapReader(Stream stream)
        {
            _stream = stream;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        public IList<(long keyId, long valId)> Read(long offset, int length)
        {
            _stream.Seek(offset, SeekOrigin.Begin);

            Span<byte> buf = stackalloc byte[length];
            int read = _stream.Read(buf);

            if (read != length)
            {
                throw new InvalidDataException();
            }

            const int blockSize = sizeof(long) + sizeof(long);
            var blockCount = length / blockSize;
            var docMapping = new List<(long, long)>();

            for (int i = 0; i < blockCount; i++)
            {
                var offs = i * blockSize;
                var key = BitConverter.ToInt64(buf.Slice(offs));
                var val = BitConverter.ToInt64(buf.Slice(offs + sizeof(long)));

                docMapping.Add((key, val));
            }

            return docMapping;
        }
    }
}
