using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Sir.Postings
{
    public class PostingsReader : IReader, ILogger
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
                // A read request is either a request to "lookup by ID" or to "execute query".

                var timer = new Stopwatch();
                timer.Start();

                var stream = new MemoryStream();

                request.Body.CopyTo(stream);

                var buf = stream.ToArray();
                Result result;
                var skip = int.Parse(request.Query["skip"]);
                var take = int.Parse(request.Query["take"]);

                if (buf.Length == 0)
                {
                    var id = long.Parse(request.Query["id"]);

                    result = await _data.Read(collectionId.ToHash(), id, skip, take);

                    this.Log("processed read request for {0} postings in {1}", result.Total, timer.Elapsed);
                }
                else
                {
                    var query = Query.FromStream(buf);

                    result = await _data.Reduce(collectionId.ToHash(), query, skip, take);

                    this.Log("processed map/reduce request resulting in {0} postings in {1}", result.Total, timer.Elapsed);
                }

                return result;
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
