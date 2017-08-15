using log4net;
using StreamIndex;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DocumentTable
{
    public class WriteSession : IWriteSession
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WriteSession));

        private readonly SegmentInfo _version;
        private readonly BlockInfoWriter _addressWriter;
        private readonly DocumentWriter _docWriter;
        private readonly Stream _docHashesStream;
        private readonly IDictionary<string, short> _keyIndex;
        private readonly List<string> _fieldNames;
        private readonly string _keyIndexFileName;
        private bool _committed;

        protected readonly string _directory;

        public SegmentInfo Version { get { return _version; } }

        public WriteSession(string directory, Compression compression)
        {
            var version = LockUtil.GetNextChronologicalFileId();

            _version = CreateNewSegmentInfo(version);
            _version.Compression = compression;

            Log.InfoFormat("begin writing {0}", _version);

            var docFileName = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName() + ".dtbl");
            var docAddressFn = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName() + ".da");
            var docHashesFileName = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName() + ".pk");

            _directory = directory;
            _keyIndexFileName = Path.Combine(Directory.GetCurrentDirectory(), Path.GetRandomFileName() + ".kix");
            _addressWriter = new BlockInfoWriter(
                    new FileStream(docAddressFn, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.None, 4096, FileOptions.DeleteOnClose));

            _docWriter = new DocumentWriter(
                new FileStream(docFileName, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.None, 4096, FileOptions.DeleteOnClose), compression);

            _docHashesStream = new FileStream(
                docHashesFileName, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.None, 4096, FileOptions.DeleteOnClose);

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

            _docWriter.Flush();
        }

        protected Stream OpenDataFileStream(out FileStream lockFile)
        {
            Stream dataFile;

            if (!LockUtil.TryAquireWriteLock(_directory, out lockFile))
            {
                var dataFileName = Path.Combine(_directory, _version + ".rdb");

                dataFile = new FileStream(
                    dataFileName,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    4096
                    );
            }
            else
            {
                var ixFileName = LockUtil.GetFirstIndexFileNameInChronologicalOrder(_directory);

                long dataFileVersion = ixFileName == null ?
                    _version.Version :
                    long.Parse(Path.GetFileNameWithoutExtension(ixFileName));

                var dataFileName = Path.Combine(_directory, dataFileVersion + ".rdb");

                dataFile = new FileStream(
                    dataFileName,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    4096
                    );
            }
            return dataFile;
        }

        public void Flush()
        {
            FileStream lockFile;
            using (var dataFile = OpenDataFileStream(out lockFile))
            {
                long startPosition = dataFile.Position;

                DoFlush(dataFile);

                _version.Length = dataFile.Position - startPosition;
            }
            lockFile.Dispose();
        }

        protected virtual void DoFlush(Stream dataFile)
        {
            _version.DocHashOffset = dataFile.Position;
            _docHashesStream.Flush();

            var timer = Stopwatch.StartNew();

            _docHashesStream.Position = 0;
            _docHashesStream.CopyTo(dataFile);

            Log.InfoFormat("copied doc primary keys to data file in {0}", timer.Elapsed);

            _version.DocAddressesOffset = dataFile.Position;
            _addressWriter.Stream.Flush();

            timer = Stopwatch.StartNew();

            _addressWriter.Stream.Position = 0;
            _addressWriter.Stream.CopyTo(dataFile);

            Log.InfoFormat("copied doc addresses to data file in {0}", timer.Elapsed);

            _version.KeyIndexOffset = dataFile.Position;
            _version.KeyIndexSize = _fieldNames.Serialize(dataFile);

            _docWriter.Stream.Flush();

            timer = Stopwatch.StartNew();

            _docWriter.Stream.Position = 0;
            _docWriter.Stream.CopyTo(dataFile);

            Log.InfoFormat("copied documents to data file in {0}", timer.Elapsed);

            _docWriter.Dispose();
            _docHashesStream.Dispose();
            _addressWriter.Dispose();
        }

        protected virtual SegmentInfo CreateNewSegmentInfo(long version)
        {
            return new SegmentInfo { Version = version };
        }

        protected virtual void SaveSegmentInfo(SegmentInfo ix)
        {
            ix.Serialize(Path.Combine(_directory, ix.Version + ".ix"));
        }

        public SegmentInfo Commit()
        {
            if (!_committed)
            {
                SaveSegmentInfo(_version);
                _committed = true;
            }
            return _version;
        }

        public void Dispose()
        {
            if (!_committed) Commit();
        }
    }


}
