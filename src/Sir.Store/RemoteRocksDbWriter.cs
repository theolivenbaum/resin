using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;

namespace Sir.Store
{
    public class RemoteRocksDbWriter : ILogger
    {
        public async Task<long> Send(byte[] payload, string url)
        {
            var timer = new Stopwatch();
            timer.Start();

            var request = (HttpWebRequest)WebRequest.Create(url);

            request.ContentType = "application/rocksdb+octet-stream";
            request.Accept = "application/octet-stream";
            request.Method = WebRequestMethods.Http.Post;
            request.ContentLength = payload.Length;

            using (var requestBody = await request.GetRequestStreamAsync())
            {
                requestBody.Write(payload, 0, payload.Length);
            }

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                using (var responseBody = response.GetResponseStream())
                {
                    this.Log(string.Format("sent {0} bytes and got a response in {1}", payload.Length, timer.Elapsed));

                    var buf = new byte[sizeof(long)];
                    var read = responseBody.Read(buf);

                    if (read == 0)
                    {
                        throw new DataMisalignedException();
                    }

                    return BitConverter.ToInt64(buf);
                }
            }
        }
    }
}
