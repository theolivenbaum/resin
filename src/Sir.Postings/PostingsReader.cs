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

        public async Task<ResponseModel> Read(string collectionId, HttpRequest request)
        {
            try
            {
                // A read request is either a request to do a "lookup by ID" or to "execute query".

                var timer = new Stopwatch();
                timer.Start();

                var stream = new MemoryStream();

                request.Body.CopyTo(stream);

                var buf = stream.ToArray();
                var skip = int.Parse(request.Query["skip"]);
                var take = int.Parse(request.Query["take"]);
                ResponseModel resultModel;

                if (buf.Length == 0)
                {
                    var ids = new List<long>();

                    foreach (var idParam in request.Query["id"])
                    {
                        var offset = long.Parse(idParam);
                        var subResult = await _data.Read(collectionId.ToHash(), offset);

                        foreach (var x in subResult)
                        {
                            ids.Add(x);
                        }
                    }

                    var sorted = ids
                        .GroupBy(x => x)
                        .Select(x => (x.Key, x.Count()))
                        .OrderByDescending(x => x.Item2)
                        .ToList();

                    var window = sorted
                        .Skip(skip)
                        .Take(take == 0 ? sorted.Count : take)
                        .Select(x => x.Item1)
                        .ToList();

                    var streamResult = StreamRepository.Serialize(window.ToDictionary(x => x, y => 0f));

                    resultModel = new ResponseModel { Stream = streamResult, MediaType = "application/postings", Total = sorted.Count };

                    this.Log("processed read request for {0} postings in {1}", ids.Count, timer.Elapsed);
                }
                else
                {
                    var query = Query.FromStream(buf);

                    resultModel = await Reduce(collectionId.ToHash(), query, skip, take);

                    this.Log("executed query in {0}", timer.Elapsed);
                }

                return resultModel;
            }
            catch (Exception ex)
            {
                this.Log(ex);

                throw;
            }
        }

        private async Task<ResponseModel> Reduce(ulong collectionId, IList<Query> query, int skip, int take)
        {
            IDictionary<long, float> result = null;

            foreach (var q in query)
            {
                var cursor = q;

                while (cursor != null)
                {
                    var docIdList = await _data.Read(collectionId, cursor.PostingsOffset);
                    var docIds = docIdList.ToDictionary(docId => docId, score => cursor.Score);

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
                    return pair1.Value.CompareTo(pair2.Value);
                }
            );

            sortedByScore.Reverse();

            if (take < 1)
            {
                take = sortedByScore.Count;
            }
            if (skip < 1)
            {
                skip = 0;
            }

            var window = sortedByScore.Skip(skip).Take(take);

            var stream = StreamRepository.Serialize(window);

            return new ResponseModel { Stream = stream, MediaType = "application/postings", Total = sortedByScore.Count };
        }

        public void Dispose()
        {
        }
    }
}
