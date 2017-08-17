using DocumentTable;
using log4net;
using Resin.IO;
using StreamIndex;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Resin
{
    public class NetworkFullTextReadSession : ReadSession, IFullTextReadSession
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(NetworkFullTextReadSession));
        private readonly Socket _socket;

        public NetworkFullTextReadSession(
            SegmentInfo version, DocHashReader docHashReader, BlockInfoReader addressReader, Stream stream) 
            : base(version, docHashReader, addressReader, stream)
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntryAsync(Dns.GetHostName()).Result;
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11100);

            _socket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            try
            {
                _socket.Connect(remoteEP);

                Log.InfoFormat("Socket connected to {0}",
                    _socket.RemoteEndPoint.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected exception : {0}", e.ToString());
            }
        }

        public IList<DocumentPosting> ReadTermCounts(IList<BlockInfo> addresses)
        {
            return new NetworkPostingsReader(_socket).ReadTermCounts(addresses);
        }

        public IList<IList<DocumentPosting>> ReadMany(IList<IList<BlockInfo>> addresses)
        {
            return new NetworkPostingsReader(_socket).ReadMany(addresses);
        }

        public IList<DocumentPosting> Read(IList<BlockInfo> addresses)
        {
            return new NetworkPostingsReader(_socket).Read(addresses);
        }

        public ScoredDocument ReadDocument(DocumentScore score)
        {
            var docAddress = AddressReader.Read(
                new BlockInfo(score.DocumentId * BlockSize, BlockSize));

            Stream.Seek(Version.KeyIndexOffset, SeekOrigin.Begin);

            var keyIndex = DocumentSerializer.ReadKeyIndex(Stream, Version.KeyIndexSize);

            using (var documentReader = new DocumentReader(
                Stream, Version.Compression, keyIndex, leaveOpen: true))
            {
                var document = documentReader.Read(docAddress);
                document.Id = score.DocumentId;
                return new ScoredDocument(document, score.Score);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            _socket.Shutdown(SocketShutdown.Both);
            _socket.Dispose();
        }
    }
}
