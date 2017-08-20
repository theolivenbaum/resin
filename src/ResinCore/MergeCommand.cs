using log4net;
using Resin.Analysis;
using StreamIndex;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Resin.Documents;

namespace Resin
{
    public class MergeCommand : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MergeCommand));

        private IList<DocHashReader> _hashReader;
        private IList<BlockInfoReader> _addressReader;
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
            _addressReader = new List<BlockInfoReader>();
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
                return 0;
            }

            // merge branches

            var branchFileName = _ixFilesToProcess[1];
            var dir = Path.GetDirectoryName(branchFileName);
            var dataFileName = Path.Combine(dir, Path.GetFileNameWithoutExtension(branchFileName) + ".rdb");
            if (File.Exists(dataFileName))
            {
                return Merge(_ixFilesToProcess[1], _ixFilesToProcess[0]);
            }

            return long.Parse(Path.GetFileNameWithoutExtension(_ixFilesToProcess[0]));
        }

        private long Truncate(string srcIxFileName)
        {
            Log.InfoFormat("truncating {0}", srcIxFileName);

            var srcIx = SegmentInfo.Load(srcIxFileName);
            var srcDataFileName = Path.Combine(_directory, srcIx.Version + ".rdb");
            long version;

            using (var source = new FileStream(srcDataFileName, FileMode.Open))
            using (var documentStream = new DocumentTableStream(source, srcIx))
            {
                using (var upsert = new FullTextUpsertTransaction(
                    _directory,
                    _analyzer,
                    srcIx.Compression,
                    documentStream))
                {
                    version = upsert.Write();
                }

                Log.InfoFormat("truncated ix {0}", version);
            }
            File.Delete(srcIxFileName);
            return version;
        }

        private long Merge(string srcIxFileName, string targetIxFileName)
        {
            Log.InfoFormat("merging branch {0} with trunk {1}", srcIxFileName, targetIxFileName);

            var srcIx = SegmentInfo.Load(srcIxFileName);
            var targetIx = SegmentInfo.Load(targetIxFileName);
            var srcDataFileName = Path.Combine(_directory, srcIx.Version + ".rdb");
            var targetDataFileName = Path.Combine(_directory, targetIx.Version + ".rdb");

            FileStream lockFile;

            if (!LockUtil.TryAquireWriteLock(_directory, out lockFile))
            {
                throw new InvalidOperationException(
                    "Cannot merge because there are other writes in progress.");
            }

            using (lockFile)
            using (var source = new FileStream(srcDataFileName, FileMode.Open))
            using (var target = new FileStream(targetDataFileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                var newStartIndex = targetIx.Length;
                var fieldOffsets = new Dictionary<ulong, long>();

                foreach (var field in srcIx.FieldOffsets)
                {
                    fieldOffsets[field.Key] = field.Value + newStartIndex;
                }
                srcIx.FieldOffsets = fieldOffsets;

                var tree = new byte[srcIx.PostingsOffset];
                var postings = new byte[srcIx.DocHashOffset - srcIx.PostingsOffset];
                var docHashes = new byte[srcIx.DocAddressesOffset - srcIx.DocHashOffset];
                var docAddresses = new byte[srcIx.KeyIndexOffset - srcIx.DocAddressesOffset];
                var documents = new byte[srcIx.Length - srcIx.KeyIndexOffset];
                var sum = tree.Length + postings.Length + docHashes.Length + docAddresses.Length + documents.Length;

                if (sum != srcIx.Length)
                {
                    throw new DataMisalignedException("Size of segment does not compute.");
                }

                source.Read(tree, 0, tree.Length);
                source.Read(postings, 0, postings.Length);
                source.Read(docHashes, 0, docHashes.Length);
                source.Read(docAddresses, 0, docAddresses.Length);
                source.Read(documents, 0, documents.Length);

                target.Write(tree, 0, tree.Length);

                srcIx.PostingsOffset = target.Position;
                target.Write(postings, 0, postings.Length);

                srcIx.DocHashOffset = target.Position;
                target.Write(docHashes, 0, docHashes.Length);

                srcIx.DocAddressesOffset = target.Position;
                target.Write(docAddresses, 0, docAddresses.Length);

                srcIx.KeyIndexOffset = target.Position;
                target.Write(documents, 0, documents.Length);

                srcIx.Serialize(srcIxFileName);

                Log.InfoFormat("merged {0} with {1} creating a segmented index", srcIxFileName, targetIx);
            }
            File.Delete(srcDataFileName);
            return srcIx.Version;
        }
    }
}