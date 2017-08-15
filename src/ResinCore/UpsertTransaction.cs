using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using Resin.Analysis;
using Resin.IO;
using System.Diagnostics;
using DocumentTable;

namespace Resin
{
    public class UpsertTransaction : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UpsertTransaction));

        private readonly string _directory;
        private readonly IAnalyzer _analyzer;
        private readonly DocumentStream _documents;
        private readonly IWriteSession _writeSession;
        private bool _flushed;
        private readonly TreeBuilder _treeBuilder;

        public UpsertTransaction(
            string directory, 
            IAnalyzer analyzer, 
            Compression compression, 
            DocumentStream documents, 
            IFullTextWriteSessionFactory storeWriterFactory = null)
        {
            _directory = directory;
            _analyzer = analyzer;
            _documents = documents;
            _treeBuilder = new TreeBuilder();

            var factory = storeWriterFactory ?? new FullTextWriteSessionFactory(directory);

            _writeSession = factory.OpenWriteSession(compression, _treeBuilder);
        }

        public long Write()
        {
            if (_flushed) return _writeSession.Version.Version;

            var docTimer = Stopwatch.StartNew();
            var upsert = new DocumentUpsertCommand(_writeSession, _analyzer, _treeBuilder);
            var count = 0;

            foreach (var doc in _documents.ReadSource())
            {
                doc.Id = count++;

                upsert.Write(doc);
            }
            _writeSession.Version.DocumentCount = count;

            Log.InfoFormat("analyzed {0} documents in {1}", count, docTimer.Elapsed);

            _writeSession.Flush();

            _writeSession.Commit();

            _flushed = true;

            return _writeSession.Version.Version;
        }

        private IEnumerable<Document> ReadSource()
        {
            return _documents.ReadSource();
        }

        public void Dispose()
        {
            _writeSession.Dispose();
        }
    }
}