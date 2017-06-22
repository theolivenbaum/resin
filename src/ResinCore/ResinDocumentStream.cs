using Resin.IO;
using Resin.IO.Read;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Resin
{
    public class ResinDocumentStream : DocumentStream, IDisposable
    {
        private readonly IEnumerable<Document> _documents;
        private readonly DocHashReader _hashReader;
        private readonly DocumentAddressReader _addressReader;
        private readonly DocumentReader _documentReader;
        private readonly IxInfo _ix;
        private IList<string> _tmpFiles;

        public ResinDocumentStream(string fileName, string primaryKeyFieldName = null) 
            : base(primaryKeyFieldName)
        {
            var versionId = Path.GetFileNameWithoutExtension(fileName);
            var directory = Path.GetDirectoryName(fileName);
            var docFileName = Path.Combine(directory, versionId + ".rdoc");
            var docAddressFn = Path.Combine(directory, versionId + ".da");
            var docHashesFileName = Path.Combine(directory, string.Format("{0}.{1}", versionId, "pk"));

            var tmpDoc = Path.Combine(directory, Path.GetRandomFileName());
            var tmpAdr = Path.Combine(directory, Path.GetRandomFileName());
            var tmpHas = Path.Combine(directory, Path.GetRandomFileName());

            File.Copy(docFileName, tmpDoc);
            File.Copy(docAddressFn, tmpAdr);
            File.Copy(docHashesFileName, tmpHas);

            _tmpFiles = new List<string>();
            _tmpFiles.Add(tmpDoc);
            _tmpFiles.Add(tmpAdr);
            _tmpFiles.Add(tmpHas);

            _ix = IxInfo.Load(Path.Combine(directory, versionId + ".ix"));
            _hashReader = new DocHashReader(tmpHas);
            _addressReader = new DocumentAddressReader(new FileStream(tmpAdr, FileMode.Open, FileAccess.Read));
            _documentReader = new DocumentReader(new FileStream(tmpDoc, FileMode.Open, FileAccess.Read), _ix.Compression);
        }

        public void Dispose()
        {
            _hashReader.Dispose();
            _addressReader.Dispose();
            _documentReader.Dispose();

            foreach(var file in _tmpFiles)
            {
                File.Delete(file);
            }
        }

        public override IEnumerable<Document> ReadSource()
        {
            return ReadSourceAndAssignPk(StreamDocuments());
        }

        private IEnumerable<Document> StreamDocuments()
        {
            for (int docId = 0; docId < _ix.DocumentCount; docId++)
            {
                var hash = _hashReader.Read(docId);

                var address = _addressReader.Read(new[]
                {
                    new BlockInfo(docId * Serializer.SizeOfBlock(), Serializer.SizeOfBlock())
                }).First();

                var document = _documentReader.Read(new List<BlockInfo> { address }).First();

                if (!hash.IsObsolete)
                {
                    yield return document;
                }
            }
        }
    }
}
