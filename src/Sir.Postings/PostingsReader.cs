using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Sir.Postings
{
    public class PostingsReader : IReader, ILogger
    {
        public string ContentType => "application/postings";

        private readonly StreamRepository _data;

        public PostingsReader(StreamRepository data)
        {
            _data = data;
        }

        public async Task<Result> Read(string collectionId, HttpRequest request)
        {
            try
            {
                // A read request is either a request to do a "lookup by ID" or to "execute query".

                var timer = new Stopwatch();
                timer.Start();

                var stream = new MemoryStream();

                request.Body.CopyTo(stream);

                var buf = stream.ToArray();
                Result result;
                var skip = int.Parse(request.Query["skip"]);
                var take = int.Parse(request.Query["take"]);

                if (buf.Length == 0)
                {
                    var id = long.Parse(request.Query["id"]);

                    result = await _data.Read(collectionId.ToHash(), id, skip, take);

                    this.Log("processed read request for {0} postings in {1}", result.Total, timer.Elapsed);
                }
                else
                {
                    var query = Query.FromStream(buf);

                    result = await Reduce(collectionId.ToHash(), query, skip, take);

                    this.Log("processed map/reduce request resulting in {0} postings in {1}", result.Total, timer.Elapsed);
                }

                return result;
            }
            catch (Exception ex)
            {
                this.Log(ex);

                throw;
            }
        }

        private async Task<Result> Reduce(ulong collectionId, IList<Query> query, int skip, int take)
        {
            var result = new Dictionary<ulong, float>();

            foreach (var cursor in query)
            {
                var docIdList = await _data.ReadAndRefreshCache(collectionId, cursor.PostingsOffset);
                var docIds = docIdList.ToDictionary(docId => docId, score => cursor.Score);

                if (cursor.And)
                {
                    var aggregatedResult = new Dictionary<ulong, float>();

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

            var sortedByScore = result.ToList();
            sortedByScore.Sort(
                delegate (KeyValuePair<ulong, float> pair1,
                KeyValuePair<ulong, float> pair2)
                {
                    return pair1.Value.CompareTo(pair2.Value);
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

            sortedByScore.Reverse();

            var window = sortedByScore.Skip(skip).Take(take);

            var stream = StreamRepository.Serialize(window);

            return new Result { Data = stream, MediaType = "application/postings", Total = sortedByScore.Count };
        }

        public void Dispose()
        {
        }
    }
}
