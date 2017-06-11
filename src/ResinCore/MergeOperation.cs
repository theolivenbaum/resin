using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Read;
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
        private IList<string> _tmpFiles;

        public MergeOperation(string directory, IAnalyzer analyzer = null)
        {
            _directory = directory;
            _analyzer = analyzer ?? new Analyzer();
            var ixs = Util.GetIndexFileNamesInChronologicalOrder(_directory).Take(2).ToList();
            _ixFilesToProcess = ixs.ToArray();

            _hashReader = new List<DocHashReader>();
            _addressReader = new List<DocumentAddressReader>();
            _documentReader = new List<DocumentReader>();
            _tmpFiles = new List<string>();
        }

        private void CloseReaders()
        {
            foreach (var r in _hashReader) r.Dispose();
            foreach (var r in _addressReader) r.Dispose();
            foreach (var r in _documentReader) r.Dispose();
        }

        public void Dispose()
        {
            CloseReaders();

            OrchestrateRemove();
        }

        private void OrchestrateRemove(int count = 0)
        {
            if (count > 3) return;

            try
            {
                foreach (var file in _tmpFiles)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                Log.Warn("unable to clean up temp files after merge");

                Thread.Sleep(100);

                OrchestrateRemove(count++);
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

        public long Merge()
        {
            if (_ixFilesToProcess.Length == 1)
            {
                // rewrite index to truncate segments

                var ix = IxInfo.Load(_ixFilesToProcess[0]);

                if (Util.IsSegmented(_ixFilesToProcess[0]))
                {
                    return Truncate(ix);
                }
                else
                {
                    return -1;
                }
            }

            // merge branches by creating new segment in base index

            return Merge(_ixFilesToProcess[1]);
        }

        private long Truncate(IxInfo ix)
        {
            Log.InfoFormat("truncating {0}", ix.VersionId);

            var documents = StreamDocuments(_ixFilesToProcess[0]);
            var documentStream = new InMemoryDocumentStream(documents, ix.PrimaryKeyFieldName);
            long version;
            var directory = Path.GetDirectoryName(_ixFilesToProcess[0]);

            Util.TryAquireWriteLock(directory);

            using (var upsert = new UpsertOperation(
                _directory,
                _analyzer,
                ix.Compression,
                documentStream))
            {
                version = upsert.Write();
                upsert.Commit();

                Util.RemoveAll(_ixFilesToProcess[0]);
            }

            Util.ReleaseFileLock(directory);

            Log.InfoFormat("ix {0} fully truncated", _ixFilesToProcess[0]);

            return version;
        }

        private long Merge(string indexFileName)
        {
            Log.InfoFormat("merging {0} with [1}", _ixFilesToProcess[1], _ixFilesToProcess[0]);

            var documents = StreamDocuments(indexFileName);
            var documentStream = new InMemoryDocumentStream(documents);
            var ix = IxInfo.Load(indexFileName);
            long version;

            using (var upsert = new UpsertOperation(
                _directory,
                _analyzer,
                ix.Compression,
                documentStream))
            {
                version = upsert.Write();
                upsert.Commit();

                Util.RemoveAll(indexFileName);
            }

            Log.InfoFormat("{0} merged with {1} creating a segmented index", indexFileName, _ixFilesToProcess[0]);

            return version;
        }

        private IEnumerable<Document> StreamDocuments(string ixFileName)
        {
            var dir = Path.GetDirectoryName(ixFileName);
            var ix = IxInfo.Load(ixFileName);

            var docFileName = Path.Combine(dir, ix.VersionId + ".rdoc");
            var docAddressFn = Path.Combine(dir, ix.VersionId + ".da");
            var docHashesFileName = Path.Combine(dir, string.Format("{0}.{1}", ix.VersionId, "pk"));

            var tmpDoc = Path.GetRandomFileName();
            var tmpAdr = Path.GetRandomFileName();
            var tmpHas = Path.GetRandomFileName();

            File.Copy(docFileName, tmpDoc);
            File.Copy(docAddressFn, tmpAdr);
            File.Copy(docHashesFileName, tmpHas);

            var hashReader = new DocHashReader(tmpHas);
            var addressReader = new DocumentAddressReader(new FileStream(tmpAdr, FileMode.Open, FileAccess.Read));
            var documentReader = new DocumentReader(new FileStream(tmpDoc, FileMode.Open, FileAccess.Read), ix.Compression);

            _hashReader.Add(hashReader);
            _addressReader.Add(addressReader);
            _documentReader.Add(documentReader);

            return StreamDocuments(hashReader, addressReader, documentReader, ix);
        }
        
        private IEnumerable<Document> StreamDocuments(
            DocHashReader hashReader, 
            DocumentAddressReader addressReader, 
            DocumentReader documentReader,
            IxInfo ix)
        {
            for (int docId = 0; docId < ix.DocumentCount; docId++)
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