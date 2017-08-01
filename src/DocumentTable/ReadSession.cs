using log4net;
using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace DocumentTable
{
    public class ReadSession : IReadSession
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ReadSession));

        private readonly DocHashReader _docHashReader;
        private readonly DocumentAddressReader _addressReader;
        private readonly int _blockSize;
        private readonly Stream _stream;

        public SegmentInfo Version { get; set; }
        public Stream Stream { get { return _stream; } }

        public ReadSession(
            SegmentInfo version, 
            DocHashReader docHashReader, 
            DocumentAddressReader addressReader, 
            Stream stream)
        {
            Version = version;

            _stream = stream;
            _docHashReader = docHashReader;
            _addressReader = addressReader;
            _blockSize = BlockSerializer.SizeOfBlock();
        }

        public DocHash ReadDocHash(int docId)
        {
            return _docHashReader.Read(docId);
        }

        public IList<Document> ReadDocuments(IList<int> documentIds)
        {
            var addresses = new List<BlockInfo>(documentIds.Count);

            foreach (var id in documentIds)
            {
                addresses.Add(new BlockInfo(id * _blockSize, _blockSize));
            }

            var docAddresses = _addressReader.Read(addresses);
            var index = 0;
            var documents = new List<Document>(documentIds.Count);

            _stream.Seek(Version.KeyIndexOffset, SeekOrigin.Begin);
            var keyIndex = TableSerializer.ReadKeyIndex(_stream, Version.KeyIndexSize);

            using (var documentReader = new DocumentReader(
                _stream, Version.Compression, keyIndex, leaveOpen: true))

                foreach (var document in documentReader.Read(docAddresses))
                {
                    document.Id = documentIds[index++];
                    documents.Add(document);
                }

            return documents;
        }

        public void Dispose()
        {
            _addressReader.Dispose();
            _docHashReader.Dispose();
        }
    }
}
