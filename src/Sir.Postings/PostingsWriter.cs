using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Postings
{
    public class PostingsWriter : IWriter
    {
        public string ContentType => "application/postings";

        private readonly StreamRepository _data;
        private readonly StreamWriter _log;

        public PostingsWriter(StreamRepository data)
        {
            _data = data;
            _log = Logging.CreateWriter("postingswriter");
        }

        public async Task<Result> Write(string collectionId, HttpRequest request)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                var payload = new MemoryStream();

                await request.Body.CopyToAsync(payload);

                if (request.ContentLength.Value != payload.Length)
                {
                    throw new DataMisalignedException();
                }

                var messageBuf = payload.ToArray();

                _log.Log(string.Format("serialized {0} bytes in {1}", messageBuf.Length, timer.Elapsed));

                timer.Restart();

                var responseStream = await _data.Write(ulong.Parse(collectionId), messageBuf);

                timer.Stop();

                _log.Log(string.Format(
                    "wrote {0} bytes in {1}: {2} bytes/ms", 
                    messageBuf.Length, timer.Elapsed, messageBuf.Length / timer.ElapsedMilliseconds));

                return new Result { Data = responseStream, MediaType = "application/octet-stream" };
            }
            catch (Exception ex)
            {
                _log.WriteLine(ex);

                throw;
            }
        }

        public void Dispose()
        {
        }
    }
}
