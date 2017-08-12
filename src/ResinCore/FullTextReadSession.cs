using DocumentTable;
using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace Resin
{
    public class FullTextReadSession : ReadSession
    {
        public FullTextReadSession(SegmentInfo version, DocHashReader docHashReader, BlockInfoReader addressReader, Stream stream) : base(version, docHashReader, addressReader, stream)
        {
        }

        public ScoredDocument ReadDocuments(DocumentScore score)
        {
            var docAddress = AddressReader.Read(
                new BlockInfo(score.DocumentId * BlockSize, BlockSize));

            Stream.Seek(Version.KeyIndexOffset, SeekOrigin.Begin);

            var keyIndex = TableSerializer.ReadKeyIndex(Stream, Version.KeyIndexSize);

            using (var documentReader = new DocumentReader(
                Stream, Version.Compression, keyIndex, leaveOpen: true))
            {
                var document = documentReader.Read(docAddress);
                document.Id = score.DocumentId;
                return new ScoredDocument(document, score.Score);
            }
        }

        public IList<ScoredDocument> ReadDocuments(IList<DocumentScore> scores)
        {
            var addresses = new List<BlockInfo>(scores.Count);

            foreach (var score in scores)
            {
                addresses.Add(new BlockInfo(score.DocumentId * BlockSize, BlockSize));
            }

            var docAddresses = AddressReader.Read(addresses);
            var index = 0;
            var documents = new List<ScoredDocument>(scores.Count);

            Stream.Seek(Version.KeyIndexOffset, SeekOrigin.Begin);

            var keyIndex = TableSerializer.ReadKeyIndex(Stream, Version.KeyIndexSize);

            using (var documentReader = new DocumentReader(
                Stream, Version.Compression, keyIndex, leaveOpen: true))

                foreach (var document in documentReader.Read(docAddresses))
                {
                    var score = scores[index++];
                    document.Id = score.DocumentId;

                    var scoredDocument = new ScoredDocument(document, score.Score);
                    documents.Add(scoredDocument);
                }

            return documents;
        }
    }
}
