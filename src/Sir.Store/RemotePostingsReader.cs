using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Read postings from HTTP endpoint.
    /// </summary>
    public class RemotePostingsReader
    {
        private IConfigurationProvider _config;
        private readonly StreamWriter _log;

        public RemotePostingsReader(IConfigurationProvider config)
        {
            _config = config;
            _log = Logging.CreateWriter("remotepostingsreader");
        }

        public IList<ulong> Read(string collectionId, long offset)
        {
            var endpoint = string.Format("{0}{1}?id={2}",
                _config.Get("postings_endpoint"), collectionId, offset);

            var request = (HttpWebRequest)WebRequest.Create(endpoint);

            request.Accept = "application/postings";
            request.Method = WebRequestMethods.Http.Get;

            var timer = new Stopwatch();
            timer.Start();

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                _log.Log("waited {0} for a response from postings service", timer.Elapsed);

                timer.Restart();

                var result = new List<ulong>();

                using (var body = response.GetResponseStream())
                {
                    var mem = new MemoryStream();
                    body.CopyTo(mem);

                    var buf = mem.ToArray();

                    var read = 0;

                    while (read < buf.Length)
                    {
                        result.Add(BitConverter.ToUInt64(buf, read));

                        read += sizeof(ulong);
                    }

                    _log.Log("serialized response of {0} bytes in {1}", read, timer.Elapsed);
                }

                return result;
            }
        }

        public async Task<IList<ulong>> ReadAsync(string collectionId, long offset)
        {
            var endpoint = string.Format("{0}{1}?id={2}",
                _config.Get("postings_endpoint"), collectionId, offset);

            var request = (HttpWebRequest)WebRequest.Create(endpoint);

            request.Accept = "application/postings";
            request.Method = WebRequestMethods.Http.Get;

            using (var response = (HttpWebResponse) await request.GetResponseAsync())
            {
                var result = new List<ulong>();

                using (var body = response.GetResponseStream())
                {
                    var mem = new MemoryStream();
                    await body.CopyToAsync(mem);   

                    var buf = mem.ToArray();

                    var read = 0;

                    while (read < buf.Length)
                    {
                        result.Add(BitConverter.ToUInt64(buf, read));

                        read += sizeof(ulong);
                    }
                }

                return result;
            }
        }
    }
}
