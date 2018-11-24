using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Sir.Postings
{
    public class PostingsWriter : IWriter
    {
        public string ContentType => "application/postings";

        private readonly StreamRepository _data;
        private readonly StreamWriter _log;
        private readonly object _sync = new object();

        public PostingsWriter(StreamRepository data)
        {
            _data = data;
            _log = Logging.CreateWriter("postingswriter");
        }

        public async Task<Result> Write(string collectionId, HttpRequest request)
        {
            try
            {
                var payload = new MemoryStream();

                await request.Body.CopyToAsync(payload);

                if (request.ContentLength.Value != payload.Length)
                {
                    throw new DataMisalignedException();
                }

                var messageBuf = payload.ToArray();

                lock (_sync)
                {
                    var responseStream = _data.Write(collectionId.ToHash(), messageBuf);

                    return new Result { Data = responseStream, MediaType = "application/octet-stream" };
                }
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
