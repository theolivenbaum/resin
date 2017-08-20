using log4net;
using Resin.Sys;
using StreamIndex;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Resin.Documents;

namespace Resin.IO
{
    public class DocumentServer : IDisposable
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(PostingsServer));

        private readonly Stream _data;
        private readonly Socket _listener;
        private readonly ConcurrentDictionary<long, byte[]> _cache;
        private readonly SegmentInfo _version;
        private readonly StreamBlockReader _reader;

        public DocumentServer(string hostName, int port, string directory, int bufferSize = 4096 * 12)
        {
            _version = Util.GetIndexVersionListInChronologicalOrder(directory)[0];

            var dataFn = Path.Combine(directory, _version.Version + ".rdb");

            _data = new FileStream(
                dataFn,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize,
                FileOptions.RandomAccess);

            _reader = new StreamBlockReader(
                _data, _version.KeyIndexOffset + _version.KeyIndexSize);

            IPHostEntry ipHostInfo = Dns.GetHostEntryAsync(hostName).Result;
            IPAddress ipAddress = ipHostInfo.AddressList[1];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            _listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            _listener.Bind(localEndPoint);
            _listener.Listen(10);

            _cache = new ConcurrentDictionary<long, byte[]>();
        }

        private byte[] FindInCache(BlockInfo address)
        {
            byte[] data;
            if (!_cache.TryGetValue(address.Position, out data))
            {
                data = ReadFromDisk(address);
                _cache.GetOrAdd(address.Position, data);
            }
            return data;
        }

        private byte[] ReadFromDisk(BlockInfo address)
        {
            byte[] data = _reader.ReadFromStream(address);
            Log.InfoFormat("read {0} bytes from DISK", data.Length);
            return data;
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
                    Log.Info("document server idle");

                    Socket handler = _listener.Accept();

                    request = new byte[sizeof(long) + sizeof(int)];

                    int received = handler.Receive(request);

                    Log.InfoFormat("received a {0} byte request", received);

                    var address = BlockSerializer.DeserializeBlock(request, 0);

                    byte[] response = FindInCache(address);

                    handler.Send(response);
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Dispose();

                    Log.InfoFormat("responded with a {0} byte document", response.Length);
                }
            }
            catch (Exception e)
            {
                Log.Info(e.ToString());
            }
        }
        
    }
}
    