using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace DocumentTable
{
    public class ReadSession : IReadSession
    {
        private readonly PostingsReader _postingsReader;
        private readonly DocHashReader _docHashReader;
        private readonly DocumentAddressReader _addressReader;
        private readonly DocumentReader _documentReader;
        private readonly int _blockSize;
        private readonly Stream _stream;

        public BatchInfo Version { get; set; }
        public Stream Stream { get { return _stream; } }

        public ReadSession(
            BatchInfo version, 
            PostingsReader postingsReader, 
            DocHashReader docHashReader, 
            DocumentAddressReader addressReader, 
            DocumentReader documentReader,
            Stream stream)
        {
            Version = version;

            _stream = stream;
            _docHashReader = docHashReader;
            _postingsReader = postingsReader;
            _addressReader = addressReader;
            _documentReader = documentReader;
            _blockSize = BlockSerializer.SizeOfBlock();
        }

        public DocHash ReadDocHash(int docId)
        {
            return _docHashReader.Read(docId);
        }

        public IList<IList<DocumentPosting>> ReadPostings(IList<Term> terms)
        {
            var addresses = new List<BlockInfo>(terms.Count);

            foreach (var term in terms)
            {
                addresses.Add(term.Word.PostingsAddress.Value);
            }

            return _postingsReader.Read(addresses);
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

            foreach (var document in _documentReader.Read(docAddresses))
            {
                document.Id = documentIds[index++];
                documents.Add(document);
            }

            return documents;
        }

        public void Dispose()
        {
            _addressReader.Dispose();
            _documentReader.Dispose();
            _docHashReader.Dispose();
            _postingsReader.Dispose();
        }
    }
}
