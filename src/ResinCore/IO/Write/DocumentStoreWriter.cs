using System.Collections.Generic;
using System.IO;

namespace Resin.IO.Write
{
    public class DocumentStoreWriter : IDocumentStoreWriter
    {
        private readonly DocumentAddressWriter _addressWriter;
        private readonly DocumentWriter _docWriter;
        private readonly Stream _docHashesStream;
        private readonly HashSet<string> _fieldNames; 

        public DocumentStoreWriter(string directory, long indexVersionId, Compression compression)
        {
            var docFileName = Path.Combine(directory, indexVersionId + ".rdoc");
            var docAddressFn = Path.Combine(directory, indexVersionId + ".da");
            var docHashesFileName = Path.Combine(directory, string.Format("{0}.{1}", indexVersionId, "pk"));

            _addressWriter = new DocumentAddressWriter(
                    new FileStream(docAddressFn, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));

            _docWriter = new DocumentWriter(
                new FileStream(docFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                compression);

            _docHashesStream = new FileStream(
                docHashesFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            _fieldNames = new HashSet<string>();
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
