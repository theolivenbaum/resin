using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Read postings from HTTP endpoint.
    /// </summary>
    public class RemotePostingsReader : ILogger
    {
        private IConfigurationProvider _config;
        private readonly string _collectionName;

        public RemotePostingsReader(IConfigurationProvider config, string collectionName)
        {
            _config = config;
            _collectionName = collectionName;
        }

        public async Task<ScoredResult> Reduce(Query queryExpression)
        {
            var endpoint = _config.Get("postings_endpoint");
            var url = string.Format("{0}{1}?skip={2}&take={3}", 
                endpoint, _collectionName, queryExpression.Skip, queryExpression.Take);

            var request = (HttpWebRequest)WebRequest.Create(url);
            var query = queryExpression.ToStream();

            request.ContentType = "application/query";
            request.Accept = "application/postings";
            request.Method = WebRequestMethods.Http.Put;
            request.ContentLength = query.Length;

            using (var requestBody = await request.GetRequestStreamAsync())
            {
                await requestBody.WriteAsync(query, 0, query.Length);

                using (var response = (HttpWebResponse) await request.GetResponseAsync())
                {
                    var result = new Dictionary<long, float>();
                    int total = 0;

                    using (var body = response.GetResponseStream())
                    {
                        var mem = new MemoryStream();
                        await body.CopyToAsync(mem);

                        var buf = mem.ToArray();

                        if (response.ContentLength != buf.Length)
                        {
                            throw new DataMisalignedException();
                        }

                        var read = 0;

                        while (read < buf.Length)
                        {
                            var docId = BitConverter.ToInt64(buf, read);

                            read += sizeof(long);

                            var score = BitConverter.ToSingle(buf, read);

                            read += sizeof(float);

                            result.Add(docId, score);
                        }

                        total = int.Parse(response.Headers["X-Total"]);
                    }

                    return new ScoredResult { Documents = result, Total = total };
                }
            }
        }

        public async Task<ICollection<long>> Read(int skip, int take, params long[] offsets)
        {
            var endpoint = string.Format("{0}{1}?skip={2}&take={3}", _config.Get("postings_endpoint"), _collectionName, skip, take);

            foreach (var offset in offsets)
            {
                endpoint += "&id=" + offset;
            }

            var request = (HttpWebRequest)WebRequest.Create(endpoint);

            request.Accept = "application/postings";
            request.Method = WebRequestMethods.Http.Get;

            using (var response = (HttpWebResponse) await request.GetResponseAsync())
            {
                var result = new Dictionary<long, float>();

                using (var body = response.GetResponseStream())
                {
                    var mem = new MemoryStream();
                    await body.CopyToAsync(mem);

                    var buf = mem.ToArray();

                    if (response.ContentLength != buf.Length)
                    {
                        throw new DataMisalignedException();
                    }

                    var read = 0;

                    while (read < buf.Length)
                    {
                        var docId = BitConverter.ToInt64(buf, read);

                        read += sizeof(long);

                        var score = BitConverter.ToSingle(buf, read);

                        read += sizeof(float);

                        result.Add(docId, score);
                    }
                }

                return result.Keys;
            }
        }
    }

    public class ScoredResult
    {
        public IDictionary<long, float> Documents { get; set; }
        public int Total { get; set; }
    }
}
