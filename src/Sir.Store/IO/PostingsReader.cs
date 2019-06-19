using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Sir.Store
{
    /// <summary>
    /// Read (reduce) postings.
    /// </summary>
    public class PostingsReader : ILogger
    {
        private readonly Stream _stream;
        private readonly MemoryMappedViewAccessor _view;
        private readonly Action<long, IDictionary<BigInteger, float>, float> _read
;
        public PostingsReader(Stream stream)
        {
            _stream = stream;
            _read = GetPostingsFromStream;
        }

        public PostingsReader(MemoryMappedViewAccessor view)
        {
            _view = view;
            _read = GetPostingsFromView;
        }

        public ScoredResult Reduce(IList<Query> query, int skip, int take)
        {
            var timer = Stopwatch.StartNew();

            IDictionary<BigInteger, float> result = null;

            foreach (var q in query)
            {
                var cursor = q;

                while (cursor != null)
                {
                    var docIds = Read(cursor.PostingsOffsets, cursor.Score);

                    if (cursor.And)
                    {
                        if (result == null)
                        {
                            result = docIds;
                        }
                        else
                        {
                            var intersection = new Dictionary<BigInteger, float>();

                            foreach (var doc in result)
                            {
                                float score;
                                if (docIds.TryGetValue(doc.Key, out score))
                                {
                                    intersection.Add(doc.Key, doc.Value + score);
                                }
                            }

                            result = intersection;
                        }
                    }
                    else if (cursor.Not)
                    {
                        if (result != null)
                        {
                            foreach (var id in docIds.Keys)
                            {
                                result.Remove(id);
                            }
                        }
                    }
                    else // Or
                    {
                        if (result == null)
                        {
                            result = docIds;
                        }
                        else
                        {
                            foreach (var doc in docIds)
                            {
                                if (result.ContainsKey(doc.Key))
                                {
                                    result[doc.Key] += doc.Value;
                                }
                            }
                        }
                    }

                    cursor = cursor.NextTermInClause;
                }
            }

            var sortedByScore = new List<KeyValuePair<BigInteger, float>>(result);
            sortedByScore.Sort(
                delegate (KeyValuePair<BigInteger, float> pair1,
                KeyValuePair<BigInteger, float> pair2)
                {
                    return pair2.Value.CompareTo(pair1.Value);
                }
            );

            var index = skip > 0 ? skip : 0;
            var count = Math.Min(sortedByScore.Count, (take > 0 ? take : sortedByScore.Count));

            this.Log("reducing {0} into {1} docs took {2}", query, sortedByScore.Count, timer.Elapsed);

            return new ScoredResult { SortedDocuments = sortedByScore.GetRange(index, count), Total = sortedByScore.Count };
        }

        private IDictionary<BigInteger, float> Read(IList<long> offsets, float score)
        {
            var result = new Dictionary<BigInteger, float>();

            foreach(var offset in offsets)
            {
                _read(offset, result, score);
            }

            return result;
        }

        private void GetPostingsFromStream(long postingsOffset, IDictionary<BigInteger, float> result, float score)
        {
            _stream.Seek(postingsOffset, SeekOrigin.Begin);

            Span<byte> buf = stackalloc byte[sizeof(long)];

            _stream.Read(buf);

            var numOfPostings = BitConverter.ToInt64(buf);

            Span<byte> listBuf = stackalloc byte[DbKeys.DocId * (int)numOfPostings];

            _stream.Read(listBuf);

            for (int i = 0; i < numOfPostings; i++)
            {
                result.Add(new BigInteger(listBuf.Slice(DbKeys.DocId * i, DbKeys.DocId).ToArray()), score);
            }
        }

        private void GetPostingsFromView(long postingsOffset, IDictionary<BigInteger, float> result, float score)
        {
            var numOfPostings = _view.ReadInt64(postingsOffset);
            var buf = new BigInteger[numOfPostings];

            _view.ReadArray(postingsOffset + buf.Length, buf, 0, buf.Length);

            foreach (var word in buf)
            {
                result.Add(word, score);
            }
        }
    }

    public class ScoredResult
    {
        public IList<KeyValuePair<BigInteger, float>> SortedDocuments { get; set; }
        public int Total { get; set; }
    }
}
