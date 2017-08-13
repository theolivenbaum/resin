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

        private readonly BlockInfoWriter _addressWriter;
        private readonly DocumentWriter _docWriter;
        private readonly Stream _docHashesStream;
        private readonly IDictionary<string, short> _keyIndex;
        private readonly List<string> _fieldNames;
        private readonly string _keyIndexFileName;
        private readonly Stream _dataFile;
        private bool _flushed;
        protected readonly SegmentInfo _ix;
        private readonly long _startPosition;
        protected readonly string _directory;

        public WriteSession(string directory, SegmentInfo ix, Stream dataFile)
        {
            var docFileName = Path.Combine(directory, ix.VersionId + ".dtbl");
            var docAddressFn = Path.Combine(directory, ix.VersionId + ".da");
            var docHashesFileName = Path.Combine(directory, string.Format("{0}.{1}", ix.VersionId, "pk"));

            _directory = directory;
            _startPosition = dataFile.Position;
            _keyIndexFileName = Path.Combine(directory, ix.VersionId + ".kix");
            _ix = ix;
            _addressWriter = new BlockInfoWriter(
                    new FileStream(docAddressFn, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.None, 4096, FileOptions.DeleteOnClose));

            _docWriter = new DocumentWriter(
                new FileStream(docFileName, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.None, 4096, FileOptions.DeleteOnClose), ix.Compression);

            _docHashesStream = new FileStream(
                docHashesFileName, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.None, 4096, FileOptions.DeleteOnClose);

            _keyIndex = new Dictionary<string, short>();
            _fieldNames = new List<string>();

            _dataFile = dataFile;
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

        public void Flush()
        {
            if (_flushed) return;

            _ix.DocHashOffset = _dataFile.Position;
            _docHashesStream.Flush();

            var timer = Stopwatch.StartNew();

            _docHashesStream.Position = 0;
            _docHashesStream.CopyTo(_dataFile);

            Log.InfoFormat("copied doc primary keys to data file in {0}", timer.Elapsed);

            _ix.DocAddressesOffset = _dataFile.Position;
            _addressWriter.Stream.Flush();

            timer = Stopwatch.StartNew();

            _addressWriter.Stream.Position = 0;
            _addressWriter.Stream.CopyTo(_dataFile);

            Log.InfoFormat("copied doc addresses to data file in {0}", timer.Elapsed);

            _ix.KeyIndexOffset = _dataFile.Position;
            _ix.KeyIndexSize = _fieldNames.Serialize(_dataFile);

            _docWriter.Stream.Flush();

            timer = Stopwatch.StartNew();

            _docWriter.Stream.Position = 0;
            _docWriter.Stream.CopyTo(_dataFile);

            Log.InfoFormat("copied documents to data file in {0}", timer.Elapsed);

            _docWriter.Dispose();
            _docHashesStream.Dispose();
            _addressWriter.Dispose();

            _ix.Length = _dataFile.Position-_startPosition;

            SaveSegmentInfo(_ix);

            _flushed = true;
        }

        protected virtual void SaveSegmentInfo(SegmentInfo ix)
        {
            ix.Serialize(Path.Combine(_directory, _ix.VersionId + ".ix"));
        }

        public void Dispose()
        {
            if (!_flushed) Flush();
        }
    }


}
