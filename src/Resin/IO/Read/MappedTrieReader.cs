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
            _reader.ReadBytes(_blockSize*count);
        }

        protected override LcrsNode Step()
        {
            if (Replay != LcrsNode.MinValue)
            {
                var replayed = Replay;
                Replay = LcrsNode.MinValue;
                return replayed;
            }

            var data = _reader.ReadBytes(_blockSize);
            
            if (data.Length < _blockSize)
            {
                return LcrsNode.MinValue;
            }

            LastRead = LcrsTrieSerializer.BytesToType<LcrsNode>(data);

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