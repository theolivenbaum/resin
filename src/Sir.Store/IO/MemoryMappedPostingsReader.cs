using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;

namespace Sir.Store
{
    /// <summary>
    /// Read (reduce) postings.
    /// </summary>
    public class MemoryMappedPostingsReader : IPostingsReader
    {
        private readonly MemoryMappedViewAccessor _view;

        public MemoryMappedPostingsReader(MemoryMappedViewAccessor view)
        {
            _view = view;
        }

        public void Reduce(IEnumerable<Query> mappedQuery, IDictionary<long, double> result)
        {
            foreach (var q in mappedQuery)
            {
                var cursor = q;

                while (cursor != null)
                {
                    var termResult = Read(cursor.PostingsOffsets);

                    if (cursor.And)
                    {
                        foreach (var hit in termResult)
                        {
                            double score;

                            if (result.TryGetValue(hit, out score))
                            {
                                result[hit] = score + cursor.Score;
                            }
                            else
                            {
                                result.Remove(hit);
                            }
                        }
                    }
                    else if (cursor.Not)
                    {
                        foreach (var doc in termResult)
                        {
                            result.Remove(doc);
                        }
                    }
                    else // Or
                    {
                        foreach (var doc in termResult)
                        {
                            double score;

                            if (result.TryGetValue(doc, out score))
                            {
                                result[doc] = score + cursor.Score;
                            }
                            else
                            {
                                result.Add(doc, cursor.Score);
                            }
                        }
                    }

                    cursor = cursor.NextTermInClause;
                }
            }
        }

        private IList<long> Read(IList<long> offsets)
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
    }
}
