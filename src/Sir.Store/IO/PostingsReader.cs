using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.Store
{
    /// <summary>
    /// Read (reduce) postings.
    /// </summary>
    public class PostingsReader : ILogger
    {
        private readonly Stream _stream;

        public PostingsReader(Stream stream)
        {
            _stream = stream;
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
                    var subResult = Read(cursor.PostingsOffsets, cursor.Score);

                    this.Log($"term {cursor.Term} exists in {subResult.Count} documents");

                    if (cursor.And)
                    {
                        if (result == null)
                        {
                            result = subResult;
                        }
                        else
                        {
                            foreach (var doc in subResult)
                            {
                                float score;

                                if (result.TryGetValue(doc.Key, out score))
                                {
                                    result[doc.Key] = score + doc.Value;
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
                            foreach (var doc in subResult.Keys)
                            {
                                result.Remove(doc);
                            }
                        }
                    }
                    else // Or
                    {
                        if (result == null)
                        {
                            result = subResult;
                        }
                        else
                        {
                            foreach (var doc in subResult)
                            {
                                float score;

                                if (result.TryGetValue(doc.Key, out score))
                                {
                                    result[doc.Key] = score + doc.Value;
                                }
                                else
                                {
                                    result.Add(doc);
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
            var count = Math.Min(sortedByScore.Count, take > 0 ? take : sortedByScore.Count);

            this.Log($"found total of {sortedByScore.Count} docs in {timer.Elapsed}. skip {skip} take {take} index {index} count {count}");

            return new ScoredResult { SortedDocuments = sortedByScore.GetRange(index, count), Total = sortedByScore.Count };
        }

        private IDictionary<long, float> Read(IList<long> offsets, float score)
        {
            var result = new Dictionary<long, float>();

            foreach (var offset in offsets)
            {
                GetPostingsFromStream(offset, result, score);
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

            var read = _stream.Read(listBuf);

            if (read < listBuf.Length)
            {
                throw new Exception("oh dear");
            }

            foreach (var word in MemoryMarshal.Cast<byte, long>(listBuf))
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
