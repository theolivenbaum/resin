using StreamIndex;
using System.Collections.Generic;
using System.IO;

namespace DocumentTable
{
    public class WriteSession : IWriteSession
    {
        private readonly DocumentAddressWriter _addressWriter;
        private readonly DocumentWriter _docWriter;
        private readonly Stream _docHashesStream;
        private readonly IDictionary<string, short> _keyIndex;
        private readonly List<string> _fieldNames;
        private readonly string _keyIndexFileName;
        private readonly Stream _compoundFile;

        public WriteSession(string directory, long indexVersionId, Compression compression)
        {
            var docFileName = Path.Combine(directory, indexVersionId + ".dtbl");
            var docAddressFn = Path.Combine(directory, indexVersionId + ".da");
            var docHashesFileName = Path.Combine(directory, string.Format("{0}.{1}", indexVersionId, "pk"));

            _keyIndexFileName = Path.Combine(directory, indexVersionId + ".kix");

            _addressWriter = new DocumentAddressWriter(
                    new FileStream(docAddressFn, FileMode.CreateNew, FileAccess.Write));

            _docWriter = new DocumentWriter(
                new FileStream(docFileName, FileMode.CreateNew, FileAccess.Write),
                compression);

            _docHashesStream = new FileStream(
                docHashesFileName, FileMode.CreateNew, FileAccess.Write);

            _keyIndex = new Dictionary<string, short>();
            _fieldNames = new List<string>();

            //_compoundFile = compoundFile;
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

            new DocumentInfo(document.Hash).Serialize(_docHashesStream);
        }

        public void Dispose()
        {
            _docWriter.Dispose();

            //_docHashesStream.Flush();
            //_docHashesStream.Position = 0;
            //_docHashesStream.CopyTo(_compoundFile);
            _docHashesStream.Dispose();

            //_addressWriter.Stream.Flush();
            //_addressWriter.Stream.Position = 0;
            //_addressWriter.Stream.CopyTo(_compoundFile);
            _addressWriter.Dispose();

            using (var fs = new FileStream(_keyIndexFileName, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(fs, TableSerializer.Encoding))
            foreach (var key in _fieldNames)
            {
                    writer.WriteLine(key);
            }
        }
    }


}
