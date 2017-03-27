using System;
using System.IO;
using System.Runtime.InteropServices;
using log4net;
using Resin.IO.Write;

namespace Resin.IO.Read
{
    public class MappedTrieReader : TrieReader, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MappedTrieReader));

        private readonly Stream _stream;
        private readonly int _blockSize;

        public MappedTrieReader(string fileName)
        {
            _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096*1, FileOptions.SequentialScan);
            _blockSize = Marshal.SizeOf(typeof (LcrsNode));

            Log.DebugFormat("opened {0}", fileName);
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

            LastRead = TrieSerializer.BytesToType<LcrsNode>(data);

            return LastRead;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}