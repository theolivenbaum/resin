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

        public PostingsWriter(StreamRepository data)
        {
            _data = data;
            _log = Logging.CreateWriter("postingswriter");
        }

        public async Task<Result> Write(string collectionId, MemoryStream request)
        {
            try
            {
                var messageBuf = request.ToArray();

                var responseStream = await _data.Write(collectionId.ToHash(), messageBuf);

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
