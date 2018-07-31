using System;
using System.IO;

namespace Sir.Store
{
    public class DocIndexWriter
    {
        private readonly Stream _stream;
        private static int _blockSize = sizeof(long)+sizeof(int);

        public DocIndexWriter(Stream stream)
        {
            _stream = stream;
        }

        public ulong GetNextDocId()
        {
            return _stream.Position == 0 ? 0 : (ulong)_stream.Position / (ulong)_blockSize;
        }

        public void Append(long offset, int len)
        {
            _stream.Write(BitConverter.GetBytes(offset), 0, sizeof(long));
            _stream.Write(BitConverter.GetBytes(len), 0, sizeof(int));
        }
    }
}
