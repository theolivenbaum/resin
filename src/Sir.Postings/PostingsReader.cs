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

        public PostingsReader(StreamRepository data)
        {
            _data = data;
        }

        public async Task<Result> Read(string collectionId, HttpRequest request)
        {
            try
            {
                var timer = new Stopwatch();
                timer.Start();

                var stream = new MemoryStream();

                request.Body.CopyTo(stream);

                var buf = stream.ToArray();

                var query = Query.FromStream(buf);
                var skip = int.Parse(request.Query["skip"]);
                var take = int.Parse(request.Query["take"]);
                var result = await _data.Reduce(collectionId.ToHash(), query, skip, take);

                Logging.Log("processed read request for {0} postings in {1}", result.Total, timer.Elapsed);

                return result;
            }
            catch (Exception ex)
            {
                Logging.Log(ex);

                throw;
            }
        }

        public void Dispose()
        {
        }
    }
}
