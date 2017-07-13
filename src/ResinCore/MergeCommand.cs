using DocumentTable;
using log4net;
using Resin.Analysis;
using Resin.Sys;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Resin
{
    public class MergeCommand : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MergeCommand));

        private IList<DocHashReader> _hashReader;
        private IList<DocumentAddressReader> _addressReader;
        private IList<DocumentReader> _documentReader;
        private readonly string _directory;
        private readonly string[] _ixFilesToProcess;
        private readonly IAnalyzer _analyzer;
        private IList<string> _tmpFiles;

        public MergeCommand(string directory, IAnalyzer analyzer = null)
        {
            _directory = directory;
            _analyzer = analyzer ?? new Analyzer();

            _ixFilesToProcess = Directory.GetFiles(directory, "*.ix")
                .Select(f => new { id = long.Parse(Path.GetFileNameWithoutExtension(f)), fileName = f })
                .OrderBy(info => info.id)
                .Select(info => info.fileName).Take(2).ToArray();

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

        public long Commit()
        {
            if (_ixFilesToProcess.Length == 1)
            {
                // truncate segments

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

            var branchFileName = _ixFilesToProcess[1];
            var dir = Path.GetDirectoryName(branchFileName);
            var dataFileName = Path.Combine(dir, Path.GetFileNameWithoutExtension(branchFileName) + ".rdb");
            if (File.Exists(dataFileName))
            {
                return Merge(_ixFilesToProcess[1]);
            }

            return long.Parse(Path.GetFileNameWithoutExtension(_ixFilesToProcess[0]));
        }

        private long Truncate(string srcIxFileName)
        {
            Log.InfoFormat("truncating {0}", srcIxFileName);

            var srcIx = BatchInfo.Load(srcIxFileName);
            var dataFileName = Path.Combine(_directory, srcIx.VersionId + ".rdb");
            long version;

            using (var stream = new FileStream(dataFileName, FileMode.Open))
            using (var documentStream = new DtblStream(stream, srcIx))
            {
                using (var upsert = new UpsertCommand(
                    _directory,
                    _analyzer,
                    srcIx.Compression,
                    documentStream))
                {
                    version = upsert.Write();
                    upsert.Commit();

                }

                Log.InfoFormat("ix {0} fully truncated", _ixFilesToProcess[0]);
            }
            Util.RemoveAll(srcIxFileName);
            return version;
        }

        private long Merge(string srcIxFileName)
        {
            Log.InfoFormat("merging branch {0} with trunk {1}", _ixFilesToProcess[1], _ixFilesToProcess[0]);

            var ix = BatchInfo.Load(srcIxFileName);
            var dataFileName = Path.Combine(_directory, ix.VersionId + ".rdb");
            long version;

            using (var stream = new FileStream(dataFileName, FileMode.Open))
            using (var documentStream = new DtblStream(stream, ix))
            {
                // TODO: instead of rewriting, copy the segments from the branch file into the main data file.
                using (var upsert = new UpsertCommand(
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