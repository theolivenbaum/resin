using System.Collections.Generic;
using System.IO;

namespace Resin.IO.Write
{
    public class DocumentStoreWriter : IDocumentStoreWriter
    {
        private readonly DocumentAddressWriter _addressWriter;
        private readonly DocumentWriter _docWriter;
        private readonly Stream _docHashesStream;
        private readonly IDictionary<string, short> _keyIndex;
        private readonly List<string> _fieldNames;
        private readonly string _keyIndexFileName;

        public DocumentStoreWriter(string directory, long indexVersionId, Compression compression)
        {
            var docFileName = Path.Combine(directory, indexVersionId + ".rdoc");
            var docAddressFn = Path.Combine(directory, indexVersionId + ".da");
            var docHashesFileName = Path.Combine(directory, string.Format("{0}.{1}", indexVersionId, "pk"));

            _keyIndexFileName = Path.Combine(directory, indexVersionId + ".kix");

            _addressWriter = new DocumentAddressWriter(
                    new FileStream(docAddressFn, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));

            _docWriter = new DocumentWriter(
                new FileStream(docFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                compression);

            _docHashesStream = new FileStream(
                docHashesFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            _keyIndex = new Dictionary<string, short>();
            _fieldNames = new List<string>();
        }
        
        public void Write(Document document)
        {
            foreach (var field in document.Fields)
            {
                if (!_keyIndex.ContainsKey(field.Key))
                {
                    var keyId = _fieldNames.Count;

                    _fieldNames.Add(field.Key);
                    _keyIndex.Add(field.Key, (short)keyId);
                }
            }

            var tableRow = document.ToTableRow(_keyIndex);

            BlockInfo adr = _docWriter.Write(tableRow);

            _addressWriter.Write(adr);

            new DocHash(document.Hash).Serialize(_docHashesStream);
        }

        public void Dispose()
        {
            _docWriter.Dispose();
            _addressWriter.Dispose();
            _docHashesStream.Dispose();

            using (var fs = new FileStream(_keyIndexFileName, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(fs, Serializer.Encoding))
            foreach (var key in _fieldNames)
            {
                    writer.WriteLine(key);
            }
        }
    }


}
