using DocumentTable;
using log4net;
using StreamIndex;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Resin
{
    public class NetworkFullTextReadSessionFactory : IReadSessionFactory, IDisposable
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(NetworkFullTextReadSessionFactory));

        private readonly string _directory;
        private readonly FileStream _data;
        private readonly IPEndPoint _ip;

        public NetworkFullTextReadSessionFactory(string hostName, int port, string directory, int bufferSize = 4096 * 12)
        {
            _directory = directory;

            var version = Directory.GetFiles(directory, "*.ix")
                .Select(f => long.Parse(Path.GetFileNameWithoutExtension(f)))
                .OrderBy(v => v).First();

            var _dataFn = Path.Combine(_directory, version + ".rdb");

            _data = new FileStream(
                _dataFn,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize,
                FileOptions.RandomAccess);

            IPHostEntry ipHostInfo = Dns.GetHostEntryAsync(hostName).Result;
            IPAddress ipAddress = ipHostInfo.AddressList[1];

            _ip = new IPEndPoint(ipAddress, port);
        }

        public IReadSession OpenReadSession(long version)
        {
            var ix = FullTextSegmentInfo.Load(Path.Combine(_directory, version + ".ix"));

            return OpenReadSession(ix);
        }

        public IReadSession OpenReadSession(SegmentInfo ix)
        {
            return new NetworkFullTextReadSession(
                _ip,
                ix,
                new DocHashReader(_data, ix.DocHashOffset),
                new BlockInfoReader(_data, ix.DocAddressesOffset),
                _data);
        }

        public void Dispose()
        {
            _data.Dispose();
        }
    }
}
