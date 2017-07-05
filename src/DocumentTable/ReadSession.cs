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

        public IEnumerable<Document> Read(IList<int> documentIds)
        {
            var addresses = documentIds
                    .Select(id => new BlockInfo(id * _blockSize, _blockSize))
                    .OrderBy(b => b.Position)
                    .ToList();

            var docAddresses = _addressReader.Read(addresses).ToList();
            var index = 0;

            foreach (var document in _documentReader.Read(docAddresses))
            {
                document.Id = documentIds[index++];
                yield return document;
            }
        }

        public void Dispose()
        {
            _addressReader.Dispose();
            _documentReader.Dispose();
        }
    }
}
