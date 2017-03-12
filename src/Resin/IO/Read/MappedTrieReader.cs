using System;
using System.IO;
using System.Runtime.InteropServices;
using Resin.IO.Write;

namespace Resin.IO.Read
{
    public class MappedTrieReader : TrieReader, IDisposable
    {
        private readonly Stream _stream;
        private readonly int _blockSize;

        public MappedTrieReader(string fileName)
        {
            _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);
            _blockSize = Marshal.SizeOf(typeof (LcrsNode));
        }

        protected override void Skip(int count)
        {
            if (count > 0)
            {
                _stream.Seek(_blockSize * count, SeekOrigin.Current);
            }
        }

        protected override LcrsNode Step()
        {
            if (Replay != LcrsNode.MinValue)
            {
                var replayed = Replay;
                Replay = LcrsNode.MinValue;
                return replayed;
            }

            var data = new byte[_blockSize];
            var read = _stream.Read(data, 0, data.Length);

            if (read < _blockSize)
            {
                return LcrsNode.MinValue;
            }

            LastRead = LcrsTrieSerializer.BytesToType<LcrsNode>(data);

            return LastRead;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}