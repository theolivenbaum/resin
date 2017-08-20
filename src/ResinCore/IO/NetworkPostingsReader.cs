using StreamIndex;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public override IList<DocumentPosting> ReadPositionsFromStream(IList<BlockInfo> addresses)
        {
            var result = new List<DocumentPosting>();

            foreach (var address in addresses)
            {
                var data = ReadOverNetwork(address);
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
                var data = ReadOverNetwork(address);
                var termCounts = Serializer.DeserializeTermCounts(data);
                result.AddRange(termCounts);
            }

            return result;
        }

        private byte[] ReadOverNetwork(BlockInfo address)
        {
            var timer = Stopwatch.StartNew();

            var requestMessage = new byte[sizeof(long) + sizeof(int)];
            var pos = BitConverter.GetBytes(address.Position);
            var len = BitConverter.GetBytes(address.Length);

            pos.CopyTo(requestMessage, 0);
            len.CopyTo(requestMessage, sizeof(long));

            var socket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            socket.Connect(_ip);

            var sent = socket.Send(requestMessage);
            var data = Read(socket, address.Length);

            socket.Shutdown(SocketShutdown.Both);
            socket.Dispose();

            Log.InfoFormat("read postings from {0} in {1}", 
                _ip, timer.Elapsed);

            return data;
        }

        private byte[] Read(Socket socket, int length)
        {
            var data = new byte[length];
            var receivedTotal = 0;
            var bufferSize = Math.Min(length, 8192);

            while (receivedTotal < length)
            {
                var size = Math.Min(length - receivedTotal, bufferSize);
                var received = socket.Receive(data, receivedTotal, size, SocketFlags.None);

                receivedTotal += received;
            }

            return data;
        }
    }
}