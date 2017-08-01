using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.Sys;
using System.Diagnostics;
using DocumentTable;
using System.Linq;

namespace Resin
{
    public class UpsertTransaction : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UpsertTransaction));

        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly DocumentStream _documents;
        private readonly IWriteSession _writeSession;
        private int _count;
        private bool _flushed;
        private readonly PostingsWriter _postingsWriter;
        private readonly FullTextSegmentInfo _ix;
        private readonly Stream _compoundFile;
        private readonly Stream _lockFile;
        private readonly bool _wordPositions;

        public UpsertTransaction(
            string directory, 
            IAnalyzer analyzer, 
            Compression compression, 
            DocumentStream documents, 
            IWriteSessionFactory storeWriterFactory = null)
        {
            _wordPositions = true; // TODO: implement writing without storing word positions

            long version = Util.GetNextChronologicalFileId();

            Log.InfoFormat("begin writing {0}", version);

            FileStream lockFile;

            if (!Util.TryAquireWriteLock(directory, out lockFile))
            {
                var compoundFileName = Path.Combine(directory, version + ".rdb");

                _compoundFile = new FileStream(
                    compoundFileName,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    4096
                    );
            }
            else
            {
                var ixFileName = Util.GetIndexFileNamesInChronologicalOrder(directory).FirstOrDefault();
                long dataFileVersion;

                if (ixFileName == null)
                {
                    dataFileVersion = version;
                }
                else
                {
                    dataFileVersion = long.Parse(Path.GetFileNameWithoutExtension(ixFileName));
                }

                var compoundFileName = Path.Combine(directory, dataFileVersion + ".rdb");

                _compoundFile = new FileStream(
                    compoundFileName,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite,
                    4096
                    );

                _lockFile = lockFile;
            }

            _directory = directory;
            _analyzer = analyzer;
            _documents = documents;

            _ix = new FullTextSegmentInfo
            {
                VersionId = version,
                Compression = compression,
                PrimaryKeyFieldName = documents.PrimaryKeyFieldName,
                WordPositions = _wordPositions
            };

            var posFileName = Path.Combine(
                _directory, string.Format("{0}.{1}", _ix.VersionId, "pos"));

            var factory = storeWriterFactory ?? new FullTextWriteSessionFactory(directory, _ix);

            _writeSession = factory.OpenWriteSession(_compoundFile);

            _postingsWriter = new PostingsWriter(
                new FileStream(
                    posFileName, 
                    FileMode.CreateNew, 
                    FileAccess.ReadWrite, 
                    FileShare.None, 
                    4096, 
                    FileOptions.DeleteOnClose
                    ));
        }

        private IDictionary<ulong, long> SerializeTries(IDictionary<ulong, LcrsTrie> tries, Stream stream)
        {
            var offsets = new Dictionary<ulong, long>();

            foreach (var t in tries)
            {
                offsets.Add(t.Key, stream.Position);

                var fileName = Path.Combine(
                    _directory, string.Format("{0}-{1}.tri", _ix.VersionId, t.Key));

                t.Value.Serialize(stream);
            }

            return offsets;
        }

        public long Write()
        {
            if (_flushed) return _ix.VersionId;

            var trieBuilder = new TreeBuilder();
            var docTimer = Stopwatch.StartNew();
            var upsert = new DocumentUpsertCommand(_writeSession, _analyzer, trieBuilder);

            foreach (var doc in _documents.ReadSource())
            {
                doc.Id = _count++;

                upsert.Write(doc);
            }

            Log.InfoFormat("stored {0} documents in {1}", _count, docTimer.Elapsed);

            var posTimer = Stopwatch.StartNew(); 
            
            var tries = trieBuilder.GetTrees();

            foreach (var trie in tries)
            {
                // decode into a list of words and set postings address
                foreach (var node in trie.Value.EndOfWordNodes())
                {
                    node.PostingsAddress = _postingsWriter.Write(node.Postings);
                }

                if (Log.IsDebugEnabled)
                {
                    foreach (var word in trie.Value.Words())
                    {
                        Log.DebugFormat("{0}\t{1}", word.Value, word.Postings.Count);
                    }
                }
            }

            Log.InfoFormat(
                "stored postings refs in trees in {0}", 
                posTimer.Elapsed);

            var treeTimer = Stopwatch.StartNew();

            _ix.FieldOffsets = SerializeTries(tries, _compoundFile);

            Log.InfoFormat("serialized trees in {0}", treeTimer.Elapsed);

            _ix.WordPositions = _wordPositions;
            _ix.PostingsOffset = _compoundFile.Position;
            _postingsWriter.Stream.Flush();
            _postingsWriter.Stream.Position = 0;
            _postingsWriter.Stream.CopyTo(_compoundFile);

            _ix.DocumentCount = _count;

            return _ix.VersionId;
        }

        private IEnumerable<Document> ReadSource()
        {
            return _documents.ReadSource();
        }

        public void Flush()
        {
            if (_flushed) return;

            _postingsWriter.Dispose();

            _writeSession.Flush();
            _writeSession.Dispose();
            _compoundFile.Dispose();

            _flushed = true;
        }

        public void Dispose()
        {
            if (!_flushed) Flush();

            if (_lockFile != null)
            {
                _lockFile.Dispose();
            }
        }
    }
}