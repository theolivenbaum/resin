using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Resin.IO.Write;

namespace Resin.IO.Read
{
    public class MappedTrieReader : TrieReader, IDisposable
    {
        private readonly int _blockSize;
        private readonly BinaryReader _reader;

        public MappedTrieReader(string fileName)
        {
            _reader = new BinaryReader(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.Unicode);
            _blockSize = Marshal.SizeOf(typeof(LcrsNode));
        }

        protected override void Skip(int count)
        {
            var buffer = new byte[_blockSize*count];
            _reader.Read(buffer, 0, buffer.Length);
        }

        protected override LcrsNode Step()
        {
            if (Replay != LcrsNode.MinValue)
            {
                var replayed = Replay;
                Replay = LcrsNode.MinValue;
                return replayed;
            }

            LcrsNode data;

            try
            {
                var buffer = new byte[_blockSize];
                _reader.Read(buffer, 0, buffer.Length);
                data = LcrsTrieSerializer.BytesToType<LcrsNode>(buffer);
            }
            catch (ArgumentException)
            {
                return LcrsNode.MinValue;
            }

            LastRead = data;
            return LastRead;
        }

        public void Dispose()
        {
            if (_reader != null)
            {
                _reader.Dispose();
            }
        }
    }
}