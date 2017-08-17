using DocumentTable;
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

        public PostingsServer(string directory, int bufferSize = 4096 * 12)
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

            // Establish the local endpoint for the socket.  
            // Dns.GetHostName returns the name of the   
            // host running the application.  
            IPHostEntry ipHostInfo = Dns.GetHostEntryAsync(Dns.GetHostName()).Result;
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11100);

            // Create a TCP/IP socket.  
            _listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and   
            // listen for incoming connections.  
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
            // Data buffer for incoming data.  
            byte[] request;

            try
            {
                // Start listening for connections.  
                while (true)
                {
                    Log.Info("Waiting for a connection...");
                    // Program is suspended while waiting for an incoming connection.  
                    Socket handler = _listener.Accept();

                    // An incoming connection needs to be processed.  
                    request = new byte[sizeof(bool) + sizeof(long) + sizeof(int)];

                    int requestLength = handler.Receive(request);
                    var isTermCountRequest = request[0] == 0;
                    var address = BlockSerializer.DeserializeBlock(request, 1);
                    byte[] response;

                    if (isTermCountRequest)
                    {
                        Log.Info("term counts requested");
                        response = new StreamPostingsReader(_data, _offset)
                            .ReadTermCountsFromStream(address);
                    }
                    else
                    {
                        Log.Info("positions requested");
                        response = new StreamPostingsReader(_data, _offset)
                            .ReadPositionsFromStream(address);
                    }

                    handler.Send(response);
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Dispose();
                }
            }
            catch (Exception e)
            {
                Log.Info(e.ToString());
            }
        }
    }
}
    