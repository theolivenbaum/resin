﻿using System;
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

        public ScoredResult Reduce(IEnumerable<Query> query, int skip, int take)
        {
            var timer = Stopwatch.StartNew();

            IDictionary<long, float> result = null;

            foreach (var q in query)
            {
                var cursor = q;

                while (cursor != null)
                {
                    var readTimer = Stopwatch.StartNew();

                    var termResult = Read(cursor.PostingsOffsets, cursor.Score);

                    this.Log($"found {termResult.Count} documents for term {cursor.Term} in {readTimer.Elapsed}");

                    if (cursor.And)
                    {
                        if (result == null)
                        {
                            result = termResult;
                        }
                        else
                        {
                            foreach (var hit in termResult)
                            {
                                float score;

                                if (result.TryGetValue(hit.Key, out score))
                                {
                                    result[hit.Key] = score + hit.Value;
                                }
                                else
                                {
                                    result.Remove(hit.Key);
                                }
                            }
                        }
                    }
                    else if (cursor.Not)
                    {
                        if (result != null)
                        {
                            foreach (var doc in termResult.Keys)
                            {
                                result.Remove(doc);
                            }
                        }
                    }
                    else // Or
                    {
                        if (result == null)
                        {
                            result = termResult;
                        }
                        else
                        {
                            foreach (var doc in termResult)
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
