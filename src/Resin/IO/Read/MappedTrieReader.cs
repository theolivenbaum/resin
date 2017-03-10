using System;
using System.IO;
using Resin.IO.Write;

namespace Resin.IO.Read
{
    public class MappedTrieReader : TrieReader, IDisposable
    {
        private readonly Stream _stream;

        public MappedTrieReader(string fileName)
        {
            _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        protected override void Skip(int count)
        {
            var buffer = new byte[LcrsTrieHelper.NodeBlockSize*count];
            _stream.Read(buffer, 0, buffer.Length);
        }

        protected override LcrsNode Step()
        {
            if (Replay != LcrsNode.MinValue)
            {
                var replayed = Replay;
                Replay = LcrsNode.MinValue;
                return replayed;
            }

            var data = new byte[LcrsTrieHelper.NodeBlockSize];
            var read = _stream.Read(data, 0, data.Length);

            if (read < LcrsTrieHelper.NodeBlockSize)
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