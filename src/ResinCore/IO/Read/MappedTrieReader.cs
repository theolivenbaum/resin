using System;
using System.IO;
using log4net;
using System.Linq;

namespace Resin.IO.Read
{
    public class MappedTrieReader : TrieReader
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MappedTrieReader));

        private readonly Stream _stream;
        private readonly int _blockSize;
        private readonly long[] _segs;
        private int _segmentIndex;

        public MappedTrieReader(string fileName)
        {
            var dir = Path.GetDirectoryName(fileName);
            var sixFileName = Path.Combine(dir, 
                Path.GetFileNameWithoutExtension(fileName) + ".six");

            _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096*1, FileOptions.SequentialScan);

            using (var sixStream = new FileStream(
                sixFileName, FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, FileOptions.SequentialScan))
            {
                var ms = new MemoryStream();
                sixStream.CopyTo(ms);
                _segs = Serializer.DeserializeLongList(ms.ToArray()).ToArray();
            }

            _blockSize = Serializer.SizeOfNode();

            Log.DebugFormat("opened {0}", fileName);
        }

        public override void GoToNextSegment()
        {
            var distance = _segs[_segmentIndex] - _stream.Position;

            if (distance > 0) _stream.Seek(distance, SeekOrigin.Current);
        }

        public override bool HasMoreSegments()
        {
            return _segs.Length > ++_segmentIndex;
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

            var node = Serializer.DeserializeNode(_stream);

            LastRead = node;

            return LastRead;
        }

        public override void Dispose()
        {
            _stream.Dispose();
        }
    }
}