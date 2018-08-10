using System;
using System.Collections.Generic;
using System.IO;

namespace Sir.Store
{
    public class PostingsWriter
    {
        private readonly Stream _stream;
        private readonly byte[] _alive;
        private readonly byte[] _dead;
        public const int BlockSize = sizeof(ulong) + sizeof(byte);

        public PostingsWriter(Stream stream)
        {
            _stream = stream;
            _alive = new byte[1];
            _dead = new byte[1];

            _alive[0] = 1;
            _dead[0] = 0;
        }

        public void Write(IEnumerable<ulong> docIds)
        {
            _stream.Seek(0, SeekOrigin.End);

            foreach (var docId in docIds)
            {
                _stream.Write(BitConverter.GetBytes(docId), 0, sizeof(ulong));
                _stream.Write(_alive, 0, sizeof(byte));
            }
        }

        public void FlagAsDeleted(long offset)
        {
            _stream.Seek(offset + sizeof(ulong), SeekOrigin.Begin);
            _stream.Write(_dead, 0, sizeof(byte));
        }
    }
}
