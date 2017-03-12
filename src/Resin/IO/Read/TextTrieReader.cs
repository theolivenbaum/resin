using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Resin.IO.Read
{
    public class TextTrieReader : TrieReader, IDisposable
    {
        private readonly TextReader _reader;
        private readonly int _blockSize;

        public TextTrieReader(string fileName)
        {
            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 16*4096, FileOptions.SequentialScan);
            _reader = new StreamReader(fs, Encoding.Unicode, false, 8*1024, false);
            _blockSize = Marshal.SizeOf(typeof(LcrsNode));
        }

        public TextTrieReader(TextReader reader)
        {
            _reader = reader;
            LastRead = LcrsNode.MinValue;
            Replay = LcrsNode.MinValue;
        }

        protected override void Skip(int count)
        {
            var buffer = new char[_blockSize * count];
            _reader.ReadBlock(buffer, 0, buffer.Length);
        }

        protected override LcrsNode Step()
        {
            if (Replay != LcrsNode.MinValue)
            {
                var replayed = Replay;
                Replay = LcrsNode.MinValue;
                return replayed;
            }

            var data = new char[_blockSize];

            if (_reader.Read(data, 0, data.Length) == 0)
            {
                return LcrsNode.MinValue;
            }

            LastRead = new LcrsNode(new string(data));

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

    //public class StreamingBitmapTrieReader : TrieReader, IDisposable
    //{
    //    private readonly Stream _stream;

    //    public StreamingBitmapTrieReader(string fileName)
    //        : this(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 4096, FileOptions.SequentialScan))
    //    {
    //    }

    //    public StreamingBitmapTrieReader(Stream stream)
    //    {
    //        _stream = stream;
    //        LastRead = LcrsNode.MinValue;
    //        Replay = LcrsNode.MinValue;
    //    }

    //    protected override void Skip(int count)
    //    {
    //        var buffer = new char[LcrsTrieHelper.NodeBlockSize * count];
    //        _reader.ReadBlock(buffer, 0, buffer.Length);
    //    }

    //    protected override LcrsNode Step()
    //    {
    //        if (Replay != LcrsNode.MinValue)
    //        {
    //            var replayed = Replay;
    //            Replay = LcrsNode.MinValue;
    //            return replayed;
    //        }

    //        var data = new char[LcrsTrieHelper.NodeBlockSize];

    //        if (_reader.Read(data, 0, data.Length) == 0)
    //        {
    //            return LcrsNode.MinValue;
    //        }

    //        LastRead = new LcrsNode(new string(data));

    //        return LastRead;
    //    }

    //    public void Dispose()
    //    {
    //        _stream.Dispose();
    //    }
    //}
}