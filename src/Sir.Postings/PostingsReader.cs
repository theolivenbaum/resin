using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

                MemoryStream data;

                if (request.Query.ContainsKey("query"))
                {
                    var stream = Convert.FromBase64String(request.Query["query"]);
                    var query = Query.FromStream(stream);

                    data = await _data.Reduce(collectionId.ToHash(), query.ToList());
                }
                else if (request.Query.ContainsKey("id"))
                {
                    var id = long.Parse(request.Query["id"]);

                    data = await _data.Read(collectionId.ToHash(), id);
                }
                else
                {
                    throw new ArgumentException("query parameters 'query' or 'id' are missing.");
                }

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
