using log4net;
using StreamIndex;
using System;
using System.IO;
using System.Linq;
using System.Net;
using Resin.Documents;

namespace Resin
{
    public class NetworkFullTextReadSessionFactory : IReadSessionFactory, IDisposable
    {
        public string DirectoryName { get { return _directory; } }

        protected static readonly ILog Log = LogManager.GetLogger(typeof(NetworkFullTextReadSessionFactory));

        private readonly string _directory;
        private readonly FileStream _data;
        private readonly IPEndPoint _postingsEndpoint;
        private readonly IPEndPoint _documentsEndpoint;

        public NetworkFullTextReadSessionFactory(string postingsHostName, int postingsPort, string documentsHostName, int documentsPort, string directory, int bufferSize = 4096 * 12)
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

            if (postingsHostName != null)
            {
                IPHostEntry postingsHost = Dns.GetHostEntryAsync(postingsHostName).Result;
                IPAddress postingsIp = postingsHost.AddressList[1];

                _postingsEndpoint = new IPEndPoint(postingsIp, postingsPort);
            }
            
            if (documentsHostName != null)
            {
                IPHostEntry documentsHost = Dns.GetHostEntryAsync(documentsHostName).Result;
                IPAddress documentsIp = documentsHost.AddressList[1];

                _documentsEndpoint = new IPEndPoint(documentsIp, documentsPort);
            }
        }

        public IReadSession OpenReadSession(long version)
        {
            var ix = FullTextSegmentInfo.Load(Path.Combine(_directory, version + ".ix"));

            return OpenReadSession(ix);
        }

        public IReadSession OpenReadSession(SegmentInfo ix)
        {
            return new NetworkFullTextReadSession(
                _postingsEndpoint,
                _documentsEndpoint,
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
