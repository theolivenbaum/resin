using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Document map stream reader, for fetching a document maps (key_id/val_id) from the document map stream.
    /// A document map is needed to re-contruct a complete document.
    /// </summary>
    public class DocReader
    {
        private readonly Stream _stream;

        public DocReader(Stream stream)
        {
            _stream = stream;
        }

        public async Task<IList<(long keyId, long valId)>> Read(long offset, int length)
        {
            _stream.Seek(offset, SeekOrigin.Begin);

            var buf = new byte[length];
            var read = await _stream.ReadAsync(buf, 0, length);

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
