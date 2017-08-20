using log4net;
using StreamIndex;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Resin.IO
{
    public class NetworkBlockReader
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(NetworkBlockReader));

        protected readonly IPEndPoint Ip;

        public NetworkBlockReader(IPEndPoint ip)
        {
            Ip = ip;
        }

        public byte[] ReadOverNetwork(BlockInfo address)
        {
            var timer = Stopwatch.StartNew();

            var requestMessage = new byte[sizeof(long) + sizeof(int)];
            var pos = BitConverter.GetBytes(address.Position);
            var len = BitConverter.GetBytes(address.Length);

            pos.CopyTo(requestMessage, 0);
            len.CopyTo(requestMessage, sizeof(long));

            var socket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            socket.Connect(Ip);

            var sent = socket.Send(requestMessage);
            var data = Read(socket, address.Length);

            socket.Shutdown(SocketShutdown.Both);
            socket.Dispose();

            Log.InfoFormat("read {0} bytes from {1} in {2}",
                data.Length, Ip, timer.Elapsed);

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
