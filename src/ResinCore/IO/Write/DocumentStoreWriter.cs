using System.IO;

namespace Resin.IO.Write
{
    public class DocumentStoreWriter : IDocumentStoreWriter
    {
        private readonly DocumentAddressWriter _addressWriter;
        private readonly DocumentWriter _docWriter;
        private readonly Stream _docHashesStream;

        public DocumentStoreWriter(
            DocumentAddressWriter docAddressWriter,
            DocumentWriter docWriter,
            Stream docHashesStream)
        {
            _addressWriter = docAddressWriter;
            _docWriter = docWriter;
            _docHashesStream = docHashesStream;
        }

        public void Write(Document document)
        {
            BlockInfo adr = _docWriter.Write(document);

            _addressWriter.Write(adr);

            new DocHash(document.Hash).Serialize(_docHashesStream);
        }

        public void Dispose()
        {
            _docWriter.Dispose();
            _addressWriter.Dispose();
            _docHashesStream.Dispose();
        }
    }
}
