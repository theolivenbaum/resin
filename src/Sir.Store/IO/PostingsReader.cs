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

        public void Reduce(IEnumerable<Query> mappedQuery, IDictionary<long, double> result)
        {
            foreach (var q in mappedQuery)
            {
                var cursor = q;

                while (cursor != null)
                {
                    var readTimer = Stopwatch.StartNew();

                    var termResult = Read(cursor.PostingsOffsets, cursor.Score);

                    this.Log($"found {termResult.Count} documents for term {cursor.Term} in {readTimer.Elapsed}");

                    if (cursor.And)
                    {
                        foreach (var hit in termResult)
                        {
                            double score;

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
                    else if (cursor.Not)
                    {
                        foreach (var doc in termResult.Keys)
                        {
                            result.Remove(doc);
                        }
                    }
                    else // Or
                    {
                        foreach (var doc in termResult)
                        {
                            double score;

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

                    cursor = cursor.NextTermInClause;
                }
            }
        }

        private IDictionary<long, double> Read(IList<long> offsets, double score)
        {
            var result = new Dictionary<long, double>();

            foreach (var offset in offsets)
            {
                GetPostingsFromStream(offset, result, score);
            }

            return result;
        }

        private void GetPostingsFromStream(long postingsOffset, IDictionary<long, double> result, double score)
        {
            _stream.Seek(postingsOffset, SeekOrigin.Begin);

            Span<byte> buf = stackalloc byte[sizeof(long)];

            _stream.Read(buf);

            var numOfPostings = BitConverter.ToInt64(buf);

            Span<byte> listBuf = new byte[sizeof(long) * (int)numOfPostings];

            var read = _stream.Read(listBuf);

            foreach (var id in MemoryMarshal.Cast<byte, long>(listBuf))
            {
                result.Add(id, score);
            }
        }
    }

    public class ScoredResult
    {
        public IList<KeyValuePair<long, double>> SortedDocuments { get; set; }
        public int Total { get; set; }
    }
}
