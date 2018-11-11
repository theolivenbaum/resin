using System.IO;
using System.Threading.Tasks;

namespace Sir.Postings
{
    public class PostingsWriter : IWriter
    {
        public string ContentType => "application/postings";

        private readonly StreamRepository _data;

        public PostingsWriter(StreamRepository data)
        {
            _data = data;
        }

        public async Task Write(string collectionId, Stream request, Stream response)
        {
            await _data.Write(collectionId.ToHash(), request, response);
        }

        public async Task Write(string collectionId, long id, Stream request, Stream response)
        {
            await _data.Write(collectionId.ToHash(), id, request, response);
        }

        public void Dispose()
        {
        }
    }
}
