using System;
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
        private readonly RemotePostingsReader _postingsReader;

        public OptimizeSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory,
            IConfigurationProvider config) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _readSession = new ReadSession(collectionName, collectionId, sessionFactory, config);
            _postingsReader = new RemotePostingsReader(config);
        }

        public async Task Optimize()
        {
            var time = Stopwatch.StartNew();
            var cols = new List<(long keyId, VectorNode column)>();

            foreach (var ixFileName in Directory.GetFiles(SessionFactory.Dir, string.Format("{0}.*.ix", CollectionId)))
            {
                var columnTime = Stopwatch.StartNew();
                var keyId = long.Parse(Path.GetFileNameWithoutExtension(ixFileName).Split('.')[1]);
                var indexReader = _readSession.CreateIndexReader(keyId);
                var pages = indexReader.ReadAllPages();

                if (pages.Count == 1)
                {
                    continue;
                }

                var optimized = new VectorNode();

                for (int i = 0; i < pages.Count; i++)
                {
                    var page = pages[i];

                    foreach (var node in page.All())
                    {
                        if (node.Ancestor == null)
                            continue;

                        var docIds = _postingsReader.Read(CollectionName, 0, 0, node.PostingsOffset);

                        node.Add(docIds);

                        optimized.Add(node, VectorNode.TermIdenticalAngle, VectorNode.TermFoldAngle);
                    }
                }

                cols.Add((keyId, optimized));

                this.Log("rebuilt {0}.{1} in {2}", CollectionId, keyId, columnTime.Elapsed);
            }

            this.Log("rebuilding {0} took {1}", CollectionId, time.Elapsed);

            foreach (var col in cols)
            {
                await SerializeColumn(col.keyId, col.column);
            }
        }

        private async Task SerializeColumn(long keyId, VectorNode column)
        {
            using (var columnWriter = new ColumnSerializer(
                CollectionId, keyId, SessionFactory, new RemotePostingsWriter(_config), "ixo", "ixop"))
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