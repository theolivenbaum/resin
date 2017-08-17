using log4net;
using StreamIndex;
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Resin.IO
{
    public class NetworkPostingsReader : PostingsReader
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(NetworkPostingsReader));

        private readonly Socket _socket;

        public NetworkPostingsReader(Socket socket)
        {
            _socket = socket;
        }

        public override IList<DocumentPosting> ReadPositionsFromStream(BlockInfo address)
        {
            var msg = new byte[sizeof(long) + sizeof(int)];
            var pos = BitConverter.GetBytes(address.Position);
            var len = BitConverter.GetBytes(address.Length);

            pos.CopyTo(msg, 0);
            len.CopyTo(msg, sizeof(long));

            var sent = _socket.Send(msg);
            var data = new byte[address.Length];
            var recieved = _socket.Receive(data);
            var postings = Serializer.DeserializePostings(data);

            return postings;
        }

        public override IList<DocumentPosting> ReadTermCountsFromStream(BlockInfo address)
        {
            var msg = new byte[sizeof(long) + sizeof(int)];
            var pos = BitConverter.GetBytes(address.Position);
            var len = BitConverter.GetBytes(address.Length);

            pos.CopyTo(msg, 0);
            len.CopyTo(msg, sizeof(long));

            var sent = _socket.Send(msg);
            var data = new byte[address.Length];
            var recieved = _socket.Receive(data);
            var postings = Serializer.DeserializeTermCounts(data);

            return postings;
        }
    }
}