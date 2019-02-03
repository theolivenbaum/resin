using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    /// <summary>
    /// Document map stream reader, for fetching a document maps (key_id/val_id) from the document map stream.
    /// A document map is needed to re-contruct a complete document.
    /// </summary>
    public class DocReader
    {
        private readonly Stream _stream;
        private readonly object _sync = new object();

        public DocReader(Stream stream)
        {
            _stream = stream;
        }

        public IList<(long keyId, long valId)> Read(long offset, int length)
        {
            byte[] buf;
            int read;

            lock (_sync)
            {
                _stream.Seek(offset, SeekOrigin.Begin);

                buf = new byte[length];
                read = _stream.Read(buf, 0, length);
            }

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
                var key = BitConverter.ToInt64(buf, offs);
                var val = BitConverter.ToInt64(buf, offs + sizeof(long));

                docMapping.Add((key, val));
            }

            return docMapping;
        }
    }
}
