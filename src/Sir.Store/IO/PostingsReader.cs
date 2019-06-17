using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
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
        private readonly Action<long, IDictionary<long, float>, float> _read
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

            IDictionary<long, float> result = null;

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
                            foreach (var doc in docIds)
                            {
                                if (result.ContainsKey(doc.Key))
                                {
                                    result[doc.Key] += doc.Value;
                                }
                                else
                                {
                                    result.Remove(doc.Key);
                                }
                            }
                        }
                    }
                    else if (cursor.Not)
                    {
                        if (result != null)
                        {
                            foreach (var id in docIds.Keys)
                            {
                                result.Remove(id, out float _);
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

            var sortedByScore = new List<KeyValuePair<long, float>>(result);
            sortedByScore.Sort(
                delegate (KeyValuePair<long, float> pair1,
                KeyValuePair<long, float> pair2)
                {
                    return pair2.Value.CompareTo(pair1.Value);
                }
            );

            var index = skip > 0 ? skip : 0;
            var count = Math.Min(sortedByScore.Count, (take > 0 ? take : sortedByScore.Count));

            this.Log("reducing {0} into {1} docs took {2}", query, sortedByScore.Count, timer.Elapsed);

            return new ScoredResult { SortedDocuments = sortedByScore.GetRange(index, count), Total = sortedByScore.Count };
        }

        private IDictionary<long, float> Read(IList<long> offsets, float score)
        {
            var result = new Dictionary<long, float>();

            foreach(var offset in offsets)
            {
                _read(offset, result, score);
            }

            return result;
        }

        private void GetPostingsFromStream(long postingsOffset, IDictionary<long, float> result, float score)
        {
            _stream.Seek(postingsOffset, SeekOrigin.Begin);

            Span<byte> buf = stackalloc byte[sizeof(long)];

            _stream.Read(buf);

            var numOfPostings = BitConverter.ToInt64(buf);

            Span<byte> listBuf = stackalloc byte[sizeof(long) * (int)numOfPostings];

            _stream.Read(listBuf);

            foreach (var word in MemoryMarshal.Cast<byte, long>(listBuf).ToArray())
            {
                result.Add(word, score);
            }
        }

        private void GetPostingsFromView(long postingsOffset, IDictionary<long, float> result, float score)
        {
            var numOfPostings = _view.ReadInt64(postingsOffset);
            var buf = new long[numOfPostings];

            _view.ReadArray(postingsOffset + sizeof(long), buf, 0, buf.Length);

            foreach (var word in buf)
            {
                result.Add(word, score);
            }
        }
    }

    public class ScoredResult
    {
        public IList<KeyValuePair<long, float>> SortedDocuments { get; set; }
        public int Total { get; set; }
    }
}
