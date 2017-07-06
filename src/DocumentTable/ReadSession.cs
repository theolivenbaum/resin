using StreamIndex;
using System.Collections.Generic;
using System.Linq;

namespace DocumentTable
{
    public class ReadSession : IReadSession
    {
        private readonly DocumentAddressReader _addressReader;
        private readonly DocumentReader _documentReader;
        private readonly int _blockSize;

        public ReadSession(DocumentAddressReader addressReader, DocumentReader documentReader)
        {
            _addressReader = addressReader;
            _documentReader = documentReader;
            _blockSize = BlockSerializer.SizeOfBlock();
        }

        public IList<Document> Read(IList<int> documentIds)
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
        }
    }
}
