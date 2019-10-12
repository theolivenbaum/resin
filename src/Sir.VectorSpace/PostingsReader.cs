using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.Store
{
    /// <summary>
    /// Read (reduce) postings.
    /// </summary>
    public class PostingsReader : Reducer, IPostingsReader
    {
        private readonly Stream _stream;

        public PostingsReader(Stream stream)
        {
            _stream = stream;
        }

        public IDictionary<long, double> ReadWithScore(IList<long> offsets, double score)
        {
            var result = new Dictionary<long, double>();

            foreach (var offset in offsets)
            {
                GetPostingsFromStream(offset, result, score);
            }

            return result;
        }

        protected override IList<long> Read(IList<long> offsets)
        {
            var list = new List<long>();

            foreach (var postingsOffset in offsets)
                GetPostingsFromStream(postingsOffset, list);

            return list;
        }

        private void GetPostingsFromStream(long postingsOffset, IDictionary<long, double> result, double score)
        {
            var list = new List<long>();

            GetPostingsFromStream(postingsOffset, list);

            foreach (var id in list)
            {
                result.Add(id, score);
            }
        }

        private void GetPostingsFromStream(long postingsOffset, IList<long> result)
        {
            _stream.Seek(postingsOffset, SeekOrigin.Begin);

            Span<byte> buf = new byte[sizeof(long)];

            _stream.Read(buf);

            var numOfPostings = BitConverter.ToInt64(buf);

            var len = sizeof(long) * (int)numOfPostings;

            Span<byte> listBuf = new byte[len];

            var read = _stream.Read(listBuf);

            if (read != len)
                throw new DataMisalignedException();

            foreach (var id in MemoryMarshal.Cast<byte, long>(listBuf))
            {
                result.Add(id);
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
