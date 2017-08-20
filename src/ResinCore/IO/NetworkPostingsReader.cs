using StreamIndex;
using System.Collections.Generic;
using System.Net;

namespace Resin.IO
{
    public class NetworkPostingsReader : PostingsReader
    {
        private readonly NetworkBlockReader _reader;

        public NetworkPostingsReader(IPEndPoint ip)
        {
            _reader = new NetworkBlockReader(ip);
        }

        public override IList<DocumentPosting> ReadPositionsFromStream(IList<BlockInfo> addresses)
        {
            var result = new List<DocumentPosting>();

            foreach (var address in addresses)
            {
                var data = _reader.ReadOverNetwork(address);
                var postings = Serializer.DeserializePostings(data);
                result.AddRange(postings);
            }

            return result;
        }

        public override IList<DocumentPosting> ReadTermCountsFromStream(IList<BlockInfo> addresses)
        {
            var result = new List<DocumentPosting>();

            foreach (var address in addresses)
            {
                var data = _reader.ReadOverNetwork(address);
                var termCounts = Serializer.DeserializeTermCounts(data);
                result.AddRange(termCounts);
            }

            return result;
        }
    }
}