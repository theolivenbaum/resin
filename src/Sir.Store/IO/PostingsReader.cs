using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            IDictionary<long, float> result = null;

            foreach (var q in query)
            {
                var cursor = q;

                while (cursor != null)
                {
                    var docIdList = Read(cursor.PostingsOffsets);
                    var docIds = docIdList.Distinct().ToDictionary(docId => docId, score => cursor.Score);

                    if (result == null)
                    {
                        result = docIds;
                    }
                    else
                    {
                        if (cursor.And)
                        {
                            var aggregatedResult = new Dictionary<long, float>();

                            foreach (var doc in result)
                            {
                                float score;

                                if (docIds.TryGetValue(doc.Key, out score))
                                {
                                    aggregatedResult[doc.Key] = score + doc.Value;
                                }
                            }

                            result = aggregatedResult;
                        }
                        else if (cursor.Not)
                        {
                            foreach (var id in docIds.Keys)
                            {
                                result.Remove(id, out float _);
                            }
                        }
                        else // Or
                        {
                            foreach (var id in docIds)
                            {
                                float score;

                                if (result.TryGetValue(id.Key, out score))
                                {
                                    result[id.Key] = score + id.Value;
                                }
                                else
                                {
                                    result.Add(id.Key, id.Value);
                                }
                            }
                        }
                    }

                    cursor = cursor.Then;
                }
            }

            var sortedByScore = result.ToList();
            sortedByScore.Sort(
                delegate (KeyValuePair<long, float> pair1,
                KeyValuePair<long, float> pair2)
                {
                    return pair2.Value.CompareTo(pair1.Value);
                }
            );

            if (take < 1)
            {
                take = sortedByScore.Count;
            }
            if (skip < 1)
            {
                skip = 0;
            }

            var window = sortedByScore.Skip(skip).Take(take).ToList();

            return new ScoredResult { Documents = result, SortedDocuments = window, Total = sortedByScore.Count };
        }

        private IList<long> Read(IList<long> offsets)
        {
            var result = new List<long>();

            foreach(var offset in offsets)
            {
                result.AddRange(GetPostings(offset));
            }

            return result;
        }

        private IList<long> GetPostings(long postingsOffset)
        {
            var postings = new List<long>();

            _stream.Seek(postingsOffset, SeekOrigin.Begin);

            var buf = new byte[sizeof(long)];

            _stream.Read(buf);

            var numOfPostings = BitConverter.ToInt64(buf);

            for (int i = 0; i < numOfPostings; i++)
            {
                _stream.Read(buf);

                postings.Add(BitConverter.ToInt64(buf));
            }

            return postings;
        }
    }

    public class ScoredResult
    {
        public IDictionary<long, float> Documents { get; set; }
        public IList<KeyValuePair<long, float>> SortedDocuments { get; set; }
        public int Total { get; set; }
    }
}
