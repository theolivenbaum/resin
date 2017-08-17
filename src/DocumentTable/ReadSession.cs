using log4net;
using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace DocumentTable
{
    public class ReadSession : IReadSession
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(ReadSession));

        protected readonly DocHashReader DocHashReader;
        protected readonly BlockInfoReader AddressReader;
        protected readonly int BlockSize;

        public SegmentInfo Version { get; set; }
        public Stream Stream { get; protected set; }

        public ReadSession(
            SegmentInfo version, 
            DocHashReader docHashReader, 
            BlockInfoReader addressReader, 
            Stream stream)
        {
            Version = version;

            Stream = stream;
            DocHashReader = docHashReader;
            AddressReader = addressReader;
            BlockSize = BlockSerializer.SizeOfBlock();
        }

        public DocHash ReadDocHash(int docId)
        {
            return DocHashReader.Read(docId);
        }

        public IList<Document> ReadDocuments(IList<int> documentIds)
        {
            var addresses = new List<BlockInfo>(documentIds.Count);

            foreach (var id in documentIds)
            {
                addresses.Add(new BlockInfo(id * BlockSize, BlockSize));
            }

            var docAddresses = AddressReader.Read(addresses);
            var index = 0;
            var documents = new List<Document>(documentIds.Count);

            Stream.Seek(Version.KeyIndexOffset, SeekOrigin.Begin);

            var keyIndex = DocumentSerializer.ReadKeyIndex(Stream, Version.KeyIndexSize);

            using (var documentReader = new DocumentReader(
                Stream, Version.Compression, keyIndex, leaveOpen: true))

                foreach (var document in documentReader.Read(docAddresses))
                {
                    document.Id = documentIds[index++];
                    documents.Add(document);
                }

            return documents;
        }

        public virtual void Dispose()
        {
            AddressReader.Dispose();
            DocHashReader.Dispose();
        }
    }
}
