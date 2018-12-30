using System;
using System.Diagnostics;
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
                var timer = new Stopwatch();
                timer.Start();

                var id = long.Parse(request.Query["id"]);
                var data = await _data.Read(collectionId.ToHash(), id);

                var result = new Result { Data = data, MediaType = "application/postings", Total = data.Length/sizeof(ulong) };

                _log.Log("created request message with {0} postings in {1}", result.Total, timer.Elapsed);

                return result;
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
