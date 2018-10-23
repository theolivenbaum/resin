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

        public async Task<Result> Read(ulong collectionId, HttpRequest request)
        {
            var id = long.Parse(request.Query["id"]);
            var result = await _data.Read(collectionId, id);

            return new Result { Data = result, MediaType = "application/octet-stream" };
        }

        public void Dispose()
        {
        }
    }
}
