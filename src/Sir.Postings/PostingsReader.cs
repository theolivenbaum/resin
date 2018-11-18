using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Sir.Postings
{
    public class PostingsReader : IReader
    {
        public string ContentType => "application/postings";

        private readonly StreamRepository _data;
        private readonly StreamWriter _log;

        public PostingsReader(StreamRepository data)
        {
            _data = data;
            _log = Logging.CreateWriter("postingsreader");
        }

        public async Task<Result> Read(string collectionId, HttpRequest request)
        {
            try
            {
                var id = long.Parse(request.Query["id"]);
                var result = await _data.Read(collectionId.ToHash(), id);

                return new Result { Data = result, MediaType = "application/postings", Total = result.Length/sizeof(ulong) };
            }
            catch (Exception ex)
            {
                _log.Write(ex);

                throw;
            }
        }

        public void Dispose()
        {
        }
    }
}
