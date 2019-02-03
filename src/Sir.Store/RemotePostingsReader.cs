using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace Sir.Store
{
    /// <summary>
    /// Read postings from HTTP endpoint.
    /// </summary>
    public class RemotePostingsReader : ILogger
    {
        private IConfigurationProvider _config;

        public RemotePostingsReader(IConfigurationProvider config)
        {
            _config = config;
        }

        public ScoredResult Reduce(string collection, byte[] query, int skip, int take)
        {
            var endpoint = string.Format("{0}{1}?skip={2}&take={3}", _config.Get("postings_endpoint"), collection, skip, take);

            var request = (HttpWebRequest)WebRequest.Create(endpoint);

            request.ContentType = "application/query";
            request.Accept = "application/postings";
            request.Method = WebRequestMethods.Http.Put;
            request.ContentLength = query.Length;

            this.Log("execute request {0}", endpoint);

            using (var requestBody = request.GetRequestStream())
            {
                requestBody.Write(query, 0, query.Length);

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    var result = new Dictionary<long, float>();
                    int total = 0;

                    using (var body = response.GetResponseStream())
                    {
                        var mem = new MemoryStream();
                        body.CopyTo(mem);

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

        public ICollection<long> Read(string collectionName, int skip, int take, params long[] offsets)
        {
            var endpoint = string.Format("{0}{1}?skip={2}&take={3}", _config.Get("postings_endpoint"), collectionName, skip, take);

            foreach (var offset in offsets)
            {
                endpoint += "&id=" + offset;
            }

            var request = (HttpWebRequest)WebRequest.Create(endpoint);

            request.Accept = "application/postings";
            request.Method = WebRequestMethods.Http.Get;

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                var result = new Dictionary<long, float>();

                using (var body = response.GetResponseStream())
                {
                    var mem = new MemoryStream();
                    body.CopyTo(mem);

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
