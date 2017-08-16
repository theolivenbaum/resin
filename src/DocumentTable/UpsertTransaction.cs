using System;
using System.Collections.Generic;
using log4net;
using System.Diagnostics;

namespace DocumentTable
{
    public class DocumentUpsertTransaction : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DocumentUpsertTransaction));

        private readonly string _directory;
        private readonly DocumentStream _documents;
        protected IWriteSession WriteSession;
        private bool _flushed;
        protected IDocumentWriteCommand DocumentWriteCommand;

        public DocumentUpsertTransaction(
            string directory,
            DocumentStream documents)
        {
            _directory = directory;
            _documents = documents;
        }

        public DocumentUpsertTransaction(
            string directory, 
            Compression compression, 
            DocumentStream documents, 
            IWriteSessionFactory writeSessionFactory = null,
            IDocumentWriteCommand documentWriteCommand = null)
            : this(directory, documents)
        {
            var factory = writeSessionFactory ?? new WriteSessionFactory(directory);

            WriteSession = factory.OpenWriteSession(compression);

            DocumentWriteCommand = documentWriteCommand ?? new DocumentWriteCommand();
        }

        public long Write()
        {
            if (_flushed) return WriteSession.Version.Version;

            var docTimer = Stopwatch.StartNew();
            var count = 0;

            foreach (var doc in _documents.ReadSource())
            {
                doc.Id = count++;

                DocumentWriteCommand.Write(doc, WriteSession);
            }

            WriteSession.Version.DocumentCount = count;

            Log.InfoFormat("analyzed {0} documents in {1}", count, docTimer.Elapsed);

            WriteSession.Flush();

            _flushed = true;

            return WriteSession.Version.Version;
        }

        private IEnumerable<Document> ReadSource()
        {
            return _documents.ReadSource();
        }

        public void Dispose()
        {
            WriteSession.Commit();

            WriteSession.Dispose();
        }
    }
}