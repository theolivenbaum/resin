using log4net;
using Resin.Analysis;
using Resin.IO.Read;
using Resin.Sys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Resin
{
    public class MergeTransaction : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MergeTransaction));

        private IList<DocumentInfoReader> _hashReader;
        private IList<DocumentAddressReader> _addressReader;
        private IList<DocumentReader> _documentReader;
        private readonly string _directory;
        private readonly string[] _ixFilesToProcess;
        private readonly IAnalyzer _analyzer;
        private IList<string> _tmpFiles;

        public MergeTransaction(string directory, IAnalyzer analyzer = null)
        {
            _directory = directory;
            _analyzer = analyzer ?? new Analyzer();
            var ixs = Util.GetIndexFileNamesInChronologicalOrder(_directory).Take(2).ToList();
            _ixFilesToProcess = ixs.ToArray();

            _hashReader = new List<DocumentInfoReader>();
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

        public long Commit()
        {
            if (_ixFilesToProcess.Length == 1)
            {
                // merge segments

                var ix = BatchInfo.Load(_ixFilesToProcess[0]);

                if (Util.IsSegmented(_ixFilesToProcess[0]))
                {
                    return Truncate(_ixFilesToProcess[0]);
                }
                else
                {
                    return -1;
                }
            }

            // merge branches

            return Merge(_ixFilesToProcess[1]);
        }

        private long Truncate(string srcIxFileName)
        {
            Log.InfoFormat("truncating {0}", srcIxFileName);

            var srcIx = BatchInfo.Load(srcIxFileName);
            var documentFileName = Path.Combine(_directory, srcIx.VersionId + ".dtbl");
            var docAddressFn = Path.Combine(_directory, srcIx.VersionId + ".da");
            var docHashesFileName = Path.Combine(_directory, string.Format("{0}.{1}", srcIx.VersionId, "pk"));
            long version;

            using (var documentStream = new DtblStream(documentFileName, srcIx.PrimaryKeyFieldName))
            {

                Util.TryAquireWriteLock(_directory);

                using (var upsert = new UpsertTransaction(
                    _directory,
                    _analyzer,
                    srcIx.Compression,
                    documentStream))
                {
                    version = upsert.Write();
                    upsert.Commit();

                }

                Util.ReleaseFileLock(_directory);

                Log.InfoFormat("ix {0} fully truncated", _ixFilesToProcess[0]);
            }
            Util.RemoveAll(srcIxFileName);
            return version;
        }

        private long Merge(string srcIxFileName)
        {
            Log.InfoFormat("merging branch {0} with trunk {1}", _ixFilesToProcess[1], _ixFilesToProcess[0]);

            var ix = BatchInfo.Load(srcIxFileName);
            var documentFileName = Path.Combine(_directory, ix.VersionId + ".dtbl");
            long version;
            using (var documentStream = new DtblStream(documentFileName, ix.PrimaryKeyFieldName))
            {
                using (var upsert = new UpsertTransaction(
                    _directory,
                    _analyzer,
                    ix.Compression,
                    documentStream))
                {
                    version = upsert.Write();
                    upsert.Commit();

                }

                Log.InfoFormat("{0} merged with {1} creating a segmented index", srcIxFileName, _ixFilesToProcess[0]);

            }
            Util.RemoveAll(srcIxFileName);
            return version;
        }
    }
}