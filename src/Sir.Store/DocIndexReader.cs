using System;
using System.IO;

namespace Sir.Store
{
    public class DocIndexReader
    {
        private readonly Stream _stream;
        private static int _blockSize = sizeof(long) + sizeof(int);

        public DocIndexReader(Stream stream)
        {
            _stream = stream;
        }

        public (long offset, int length) Read(ulong docId)
        {
            var offs = (long) docId * _blockSize;

            _stream.Seek(offs, SeekOrigin.Begin);

            var buf = new byte[_blockSize];
            var read = _stream.Read(buf, 0, _blockSize);

            if (read == 0)
            {
                throw new ArgumentException("document record does not exist", nameof(docId));
            }

            return (BitConverter.ToInt64(buf, 0), BitConverter.ToInt32(buf, sizeof(long)));
        }
    }
}
