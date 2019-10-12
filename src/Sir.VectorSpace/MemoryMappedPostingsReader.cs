using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;

namespace Sir.Store
{
    /// <summary>
    /// Read (reduce) postings.
    /// </summary>
    public class MemoryMappedPostingsReader : Reducer, IPostingsReader
    {
        private readonly MemoryMappedViewAccessor _view;

        public MemoryMappedPostingsReader(MemoryMappedViewAccessor view)
        {
            _view = view;
        }
        
        protected override IList<long> Read(IList<long> offsets)
        {
            var result = new List<long>();

            foreach (var offset in offsets)
            {
                result.AddRange(Read(offset));
            }

            return result;
        }

        private IList<long> Read(long postingsOffset)
        {
            var numOfPostings = _view.ReadInt64(postingsOffset);

            var listBuf = new long[numOfPostings];

            var read = _view.ReadArray(postingsOffset + sizeof(long), listBuf, 0, listBuf.Length);

            if (read != numOfPostings)
                throw new DataMisalignedException();

            return listBuf;
        }

        public void Dispose()
        {
            _view.Dispose();
        }

        public IDictionary<long, double> ReadWithScore(IList<long> offsets, double score)
        {
            var result = new Dictionary<long, double>();

            foreach (var x in Read(offsets))
            {
                result.Add(x, score);
            }

            return result;
        }
    }
}