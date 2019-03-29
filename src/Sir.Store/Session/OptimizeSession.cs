using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
            _postingsWriter = new RemotePostingsWriter(config, collectionName);
        }

        public void Optimize()
        {
            var time = Stopwatch.StartNew();
            var optimizedColumns = new ConcurrentBag<(long keyId, VectorNode column)>();

            Parallel.ForEach(Directory.GetFiles(
                SessionFactory.Dir, string.Format("{0}.*.ix", CollectionId)), ixFileName =>
            {
                var columnTime = Stopwatch.StartNew();
                var keyId = long.Parse(Path.GetFileNameWithoutExtension(ixFileName).Split('.')[1]);
                var indexReader = _readSession.CreateIndexReader(keyId);

                indexReader.Optimize();

                optimizedColumns.Add((keyId, indexReader.Root));

                this.Log("optimized {0} in memory in {1}", keyId, columnTime.Elapsed);
            });

            Parallel.ForEach(optimizedColumns, col =>
            {
                var columnTime = Stopwatch.StartNew();

                SerializeColumn(col.keyId, col.column);

                this.Log("serialized {0} in {1}", col.keyId, columnTime.Elapsed);
            });

            this.Log("rebuilding {0} took {1}", CollectionId, time.Elapsed);
        }

        private void SerializeColumn(long keyId, VectorNode column)
        {
            var columnWriter = new ColumnSerializer(
                CollectionId,
                keyId,
                SessionFactory,
                postingsWriter: _postingsWriter,
                ixFileExtension: "ixo",
                pageFileExtension: "ixop");

            columnWriter.AppendColumnSegment(column);
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