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
            _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.RandomAccess);
        }

        protected override void Skip(int count)
        {
            if (count > 0)
            {
                _stream.Seek(LcrsTrieHelper.NodeBlockSize * count, SeekOrigin.Current);
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