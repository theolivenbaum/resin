using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.Store
{
    public class OptimizeSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly ReadSession _readSession;
        private readonly RemotePostingsWriter _postingsWriter;

        public OptimizeSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory,
            IConfigurationProvider config,
            ConcurrentDictionary<long, NodeReader> indexReaders) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _readSession = new ReadSession(collectionName, collectionId, sessionFactory, config, indexReaders);
            _postingsWriter= new RemotePostingsWriter(config, collectionName);
        }

        public async Task Optimize()
        {
            var time = Stopwatch.StartNew();
            var optimizedColumns = new List<(long keyId, VectorNode column)>();

            Parallel.ForEach(Directory.GetFiles(SessionFactory.Dir, string.Format("{0}.*.ix", CollectionId)), ixFileName =>
            {
                var columnTime = Stopwatch.StartNew();
                var keyId = long.Parse(Path.GetFileNameWithoutExtension(ixFileName).Split('.')[1]);
                var indexReader = _readSession.CreateIndexReader(keyId);
                var optimized = indexReader.ReadAllPages();

            });

            this.Log("rebuilding {0} took {1}", CollectionId, time.Elapsed);

            foreach (var col in optimizedColumns)
            {
                await SerializeColumn(col.keyId, col.column);
            }
        }

        private async Task SerializeColumn(long keyId, VectorNode column)
        {
            using (var columnWriter = new ColumnSerializer(
                CollectionId, keyId, SessionFactory, ixFileExtension: "ixo", pageFileExtension: "ixop"))
            {
                await columnWriter.SerializeColumnSegment(column);
            }
        }

        private void Publish()
        {
            this.Log("publishing");

            var time = Stopwatch.StartNew();

            var optimized = Directory.GetFiles(SessionFactory.Dir, string.Format("{0}.*.ixo", CollectionId));

            foreach (var file in optimized)
            {
                File.Replace(file, file.Replace(".ixo", ".ix"), null);
                File.Replace(file.Replace(".ixo", ".ixop"), file.Replace(".ixo", ".ixp"), null);
            }

            this.Log("publish of {0} took {1}", CollectionName, time.Elapsed);
        }

        public void Dispose()
        {
            _readSession.Dispose();

            Publish();
        }
    }
}