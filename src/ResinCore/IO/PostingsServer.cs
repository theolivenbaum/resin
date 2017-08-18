using log4net;
using Resin.Sys;
using StreamIndex;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Resin.IO
{
    public class PostingsServer : IDisposable
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(PostingsServer));

        private readonly Stream _data;
        private readonly Socket _listener;
        private long _offset;

        public PostingsServer(string hostName, int port, string directory, int bufferSize = 4096 * 12)
        {
            var version = Util.GetIndexVersionListInChronologicalOrder(directory)[0];

            _offset = version.PostingsOffset;

            var dataFn = Path.Combine(directory, version.Version + ".rdb");

            _data = new FileStream(
                dataFn,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize,
                FileOptions.RandomAccess);

            IPHostEntry ipHostInfo = Dns.GetHostEntryAsync(hostName).Result;
            IPAddress ipAddress = ipHostInfo.AddressList[1];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            _listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            _listener.Bind(localEndPoint);
            _listener.Listen(10);
        }

        public void Dispose()
        {
            if (_listener != null) _listener.Dispose();
            if (_data != null) _data.Dispose();
        }

        public void Start()
        {
            byte[] request;

            try
            {
                while (true)
                {
                    Log.Info("postings server idle");

                    Socket handler = _listener.Accept();

                    request = new byte[sizeof(long) + sizeof(int)];

                    int received = handler.Receive(request);

                    Log.InfoFormat("received a {0} byte request", received);

                    var address = BlockSerializer.DeserializeBlock(request, 0);

                    byte[] response = new StreamPostingsReader(_data, _offset)
                        .ReadPositionsFromStream(address);

                    handler.Send(response);
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Dispose();

                    Log.InfoFormat("responded with a {0} byte postings list", response.Length);
                }
            }
            catch (Exception e)
            {
                Log.Info(e.ToString());
            }
        }
    }
}
    