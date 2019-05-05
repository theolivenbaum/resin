using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
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

        public async Task<ResponseModel> Write(string collectionName, HttpRequest request)
        {
            try
            {
                var collectionId = collectionName.ToHash();
                var timer = Stopwatch.StartNew();

                var payload = new MemoryStream();

                await request.Body.CopyToAsync(payload);

                if (request.ContentLength.Value != payload.Length)
                {
                    throw new DataMisalignedException();
                }

                var compressed = payload.ToArray();
                var messageBuf = QuickLZ.decompress(compressed);

                // A write request is either a request to write new data
                // or a request to concat two or more existing pages.

                this.Log(string.Format("serialized {0} bytes in {1}", messageBuf.Length, timer.Elapsed));

                timer.Restart();

                MemoryStream responseStream;

                lock (Sync)
                {
                    this.Log("waited for synchronization for {0}", timer.Elapsed);

                    timer.Restart();

                    responseStream = _data.Write(collectionId, messageBuf);

                    timer.Stop();

                    var t = timer.ElapsedMilliseconds > 0 ? timer.ElapsedMilliseconds : 1;

                    this.Log(string.Format(
                        "wrote {0} bytes in {1}: {2} bytes/ms",
                        messageBuf.Length, timer.Elapsed, messageBuf.Length / t));
                }

                return new ResponseModel { Stream = responseStream, MediaType = "application/octet-stream" };
            }
            catch (Exception ex)
            {
                this.Log(ex);

                throw;
            }
        }

        private IDictionary<long, IList<long>> Deserialize(byte[] buf)
        {
            var result = new Dictionary<long, IList<long>>();
            var read = 0;

            while (read < buf.Length)
            {
                var canonical = BitConverter.ToInt32(buf, read);

                read += sizeof(long);

                var count = BitConverter.ToInt32(buf, read);

                read += sizeof(int);

                var list = new List<long>();

                for (int i = 0; i < count; i++)
                {
                    list.Add(BitConverter.ToInt64(buf, read));

                    read += sizeof(long);
                }

                result.Add(canonical, list);
            }

            return result;
        }

        public void Dispose()
        {
        }
    }
}