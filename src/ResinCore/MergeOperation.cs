using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Read;
using Resin.IO.Write;
using Resin.Sys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Resin
{
    public class MergeOperation : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MergeOperation));

        private IList<DocHashReader> _hashReader;
        private IList<DocumentAddressReader> _addressReader;
        private IList<DocumentReader> _documentReader;
        private readonly string _directory;
        private readonly string[] _ixFilesToProcess;
        private readonly IAnalyzer _analyzer;

        public MergeOperation(string directory, IAnalyzer analyzer = null)
        {
            _directory = directory;
            _analyzer = analyzer ?? new Analyzer();
            var ixs = Util.GetIndexFileNamesInChronologicalOrder(_directory).Take(2).ToList();
            _ixFilesToProcess = ixs.ToArray();

            _hashReader = new List<DocHashReader>();
            _addressReader = new List<DocumentAddressReader>();
            _documentReader = new List<DocumentReader>();
        }

        public void Dispose()
        {
            foreach (var r in _hashReader) r.Dispose();
            foreach (var r in _addressReader) r.Dispose();
            foreach (var r in _documentReader) r.Dispose();

            OrchestrateRemove();
        }

        private void OrchestrateRemove(int count = 0)
        {
            if (count > 3) return;

            if (_ixFilesToProcess.Length == 2)
            {
                if (!TryRemove(_ixFilesToProcess[1]))
                {
                    Thread.Sleep(100);

                    OrchestrateRemove(count++);
                }
            }
        }

        private bool TryRemove(string ixFileName)
        {
            try
            {
                File.Delete(ixFileName);

                var dir = Path.GetDirectoryName(ixFileName);
                var name = Path.GetFileNameWithoutExtension(ixFileName);

                foreach (var file in Directory.GetFiles(dir, name + ".*"))
                {
                    File.Delete(file);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public long Merge (Compression compression, string primaryKeyFieldName)
        {
            if (_ixFilesToProcess.Length == 1)
            {
                // truncate only if segmented or new compression is to be applied

                var ix = IxInfo.Load(_ixFilesToProcess[0]);

                if (Util.IsSegmented(_ixFilesToProcess[0]) || compression != ix.Compression)
                {
                    var documents = StreamDocuments(_ixFilesToProcess[0]);

                    var documentStream = new InMemoryDocumentStream(documents);

                    using (var upsert = new UpsertOperation(
                        _directory,
                        _analyzer,
                        compression,
                        documentStream))
                    {
                        return upsert.Write();
                    }
                }
            }

            // merge

            return Merge(
                _ixFilesToProcess[0],
                _ixFilesToProcess[1],
                compression,
                primaryKeyFieldName);
        }

        public long Merge(
            string firstIndexFileName, 
            string secondIndexFileName, 
            Compression compression, 
            string primaryKeyFieldName)
        {
            Log.Info("merging");

            var documents = StreamDocuments(secondIndexFileName);

            var documentStream = new InMemoryDocumentStream(documents);
            long versionId;

            using (var upsert = new UpsertOperation(
                _directory,
                new Analyzer(),
                compression,
                documentStream))
            {
                versionId = upsert.Write();
            }

            return versionId;
        }

        private IEnumerable<Document> StreamDocuments(string ixFileName)
        {
            var dir = Path.GetDirectoryName(ixFileName);
            var ix = IxInfo.Load(ixFileName);
            var docFileName = Path.Combine(dir, ix.VersionId + ".rdoc");
            var docAddressFn = Path.Combine(dir, ix.VersionId + ".da");
            var docHashesFileName = Path.Combine(dir, string.Format("{0}.{1}", ix.VersionId, "pk"));

            return StreamDocuments(
                docFileName, docAddressFn, docHashesFileName, ix.DocumentCount, ix.Compression);
        }

        private IEnumerable<Document> StreamDocuments(
            string docFileName, 
            string docAddressFn, 
            string docHashesFileName, 
            int numOfDocs,
            Compression compression)
        {
            var hashReader = new DocHashReader(docHashesFileName);
            var addressReader = new DocumentAddressReader(new FileStream(docAddressFn, FileMode.Open, FileAccess.Read));
            var documentReader = new DocumentReader(new FileStream(docFileName, FileMode.Open, FileAccess.Read), compression);

            _hashReader.Add(hashReader);
            _addressReader.Add(addressReader);
            _documentReader.Add(documentReader);

            return StreamDocuments(hashReader, addressReader, documentReader, numOfDocs);
        }

        private IEnumerable<Document> StreamDocuments(
            DocHashReader hashReader, 
            DocumentAddressReader addressReader, 
            DocumentReader documentReader,
            int numOfDocs)
        {
            for (int docId = 0; docId < numOfDocs; docId++)
            {
                var hash = hashReader.Read(docId);

                var address = addressReader.Read(new[] 
                {
                    new BlockInfo(docId * Serializer.SizeOfBlock(), Serializer.SizeOfBlock())
                }).First();

                var document = documentReader.Read(new List<BlockInfo> { address }).First();

                if (!hash.IsObsolete)
                {
                    yield return document;
                }
            }
        }
    }
}