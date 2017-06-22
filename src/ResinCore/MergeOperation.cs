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

            OrchestrateRemoveTmpFiles();
        }

        private void OrchestrateRemoveTmpFiles(int count = 0)
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

                OrchestrateRemoveTmpFiles(count++);
            }
        }

        //private bool TryRemove(string ixFileName)
        //{
        //    try
        //    {
        //        File.Delete(ixFileName);

        //        var dir = Path.GetDirectoryName(ixFileName);
        //        var name = Path.GetFileNameWithoutExtension(ixFileName);

        //        foreach (var file in Directory.GetFiles(dir, name + ".*"))
        //        {
        //            File.Delete(file);
        //        }

        //        return true;
        //    }
        //    catch
        //    {
        //        return false;
        //    }
        //}

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

            var tmpDoc = Path.Combine(_directory, Path.GetRandomFileName());
            var tmpAdr = Path.Combine(_directory, Path.GetRandomFileName());
            var tmpHas = Path.Combine(_directory, Path.GetRandomFileName());

            var documentFileName = Path.Combine(_directory, ix.VersionId + ".rdoc");
            var docAddressFn = Path.Combine(_directory, ix.VersionId + ".da");
            var docHashesFileName = Path.Combine(_directory, string.Format("{0}.{1}", ix.VersionId, "pk"));

            File.Copy(documentFileName, tmpDoc);
            File.Copy(docAddressFn, tmpAdr);
            File.Copy(docHashesFileName, tmpHas);

            var sourceIx = IxInfo.Load(_ixFilesToProcess[0]);
            using (var documentStream = new ResinDocumentStream(documentFileName, sourceIx.PrimaryKeyFieldName))
            {
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
        }

        private long Merge(string indexFileName)
        {
            Log.InfoFormat("merging {0} with [1}", _ixFilesToProcess[1], _ixFilesToProcess[0]);

            var ix = IxInfo.Load(indexFileName);
            var documentFileName = Path.Combine(_directory, ix.VersionId + ".rdoc");
            using (var documentStream = new ResinDocumentStream(documentFileName, ix.PrimaryKeyFieldName))
            {
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
        }
    }
}