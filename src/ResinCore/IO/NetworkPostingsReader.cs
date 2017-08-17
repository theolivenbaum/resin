using StreamIndex;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Resin.IO
{
    public class NetworkPostingsReader : PostingsReader
    {
        private readonly IPEndPoint _ip;

        public NetworkPostingsReader(IPEndPoint ip)
        {
            _ip = ip;
        }

        public override IList<DocumentPosting> ReadPositionsFromStream(BlockInfo address)
        {
            var data = ReadOverNetwork(address);
            var postings = Serializer.DeserializePostings(data);

            return postings;
        }

        public override IList<DocumentPosting> ReadTermCountsFromStream(BlockInfo address)
        {
            var data = ReadOverNetwork(address);
            var termCounts = Serializer.DeserializeTermCounts(data);

            return termCounts;
        }

        private byte[] ReadOverNetwork(BlockInfo address)
        {
            var msg = new byte[sizeof(long) + sizeof(int)];
            var pos = BitConverter.GetBytes(address.Position);
            var len = BitConverter.GetBytes(address.Length);

            pos.CopyTo(msg, 0);
            len.CopyTo(msg, sizeof(long));

            var socket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(_ip);

            Log.InfoFormat("fetching postings from {0}", socket.RemoteEndPoint.ToString());

            var sent = socket.Send(msg);
            var data = new byte[address.Length];
            var recieved = socket.Receive(data);

            socket.Shutdown(SocketShutdown.Both);
            socket.Dispose();

            return data;
        }
    }
}