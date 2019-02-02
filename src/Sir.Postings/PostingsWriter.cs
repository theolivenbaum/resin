using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Postings
{
    public class PostingsWriter : IWriter, ILogger
    {
        public string ContentType => "application/postings";

        private readonly StreamRepository _data;

        public PostingsWriter(StreamRepository data)
        {
            _data = data;
        }

        private static object Sync = new object();

        public async Task<ResultModel> Write(string collectionId, HttpRequest request)
        {
            try
            {
                var timer = Stopwatch.StartNew();

                var payload = new MemoryStream();

                await request.Body.CopyToAsync(payload);

                if (request.ContentLength.Value != payload.Length)
                {
                    throw new DataMisalignedException();
                }

                var compressed = payload.ToArray();
                var messageBuf = QuickLZ.decompress(compressed);

                this.Log(string.Format("serialized {0} bytes in {1}", messageBuf.Length, timer.Elapsed));

                timer.Restart();

                MemoryStream responseStream;

                lock (Sync)
                {
                    this.Log("waited for synchronization for {0}", timer.Elapsed);

                    timer.Restart();

                    responseStream = _data.Write(ulong.Parse(collectionId), messageBuf);

                    timer.Stop();

                    var t = timer.ElapsedMilliseconds > 0 ? timer.ElapsedMilliseconds : 1;

                    this.Log(string.Format(
                        "wrote {0} bytes in {1}: {2} bytes/ms",
                        messageBuf.Length, timer.Elapsed, messageBuf.Length / t));
                }

                return new ResultModel { Data = responseStream, MediaType = "application/octet-stream" };
            }
            catch (Exception ex)
            {
                this.Log(ex);

                throw;
            }
        }

        public void Dispose()
        {
        }
    }
}
