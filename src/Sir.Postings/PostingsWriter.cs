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

        public async Task<long> Write(string collectionId, Stream payload)
        {
            return await _data.Write(collectionId.ToHash(), (MemoryStream)payload);
        }

        public async Task Write(string collectionId, long id, Stream payload)
        {
            await _data.Write(collectionId.ToHash(), (MemoryStream)payload, id);
        }

        public void Dispose()
        {
        }
    }
}
