using System;
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
        private readonly IList<Task> _writeTasks;
        private readonly RemotePostingsReader _postingsReader;

        public OptimizeSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory,
            IConfigurationProvider config) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _readSession = new ReadSession(collectionName, collectionId, sessionFactory, config);
            _writeTasks = new List<Task>();
            _postingsReader = new RemotePostingsReader(config);
        }

        public void Optimize()
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

                var optimized = pages[0];

                for (int i = 1; i < pages.Count; i++)
                {
                    var page = pages[i];

                    foreach (var node in page.All())
                    {
                        if (node.Ancestor == null)
                            continue;

                        var docIds = _postingsReader.Read(CollectionName, 0, 0, node.PostingsOffset);

                        node.Merge(docIds);

                        optimized.Add(node, VectorNode.TermIdenticalAngle, VectorNode.TermFoldAngle);
                    }
                }

                cols.Add((keyId, optimized));

                this.Log("rebuilt {0}.{1} in {2}", CollectionId, keyId, columnTime.Elapsed);
            }

            this.Log("rebuilding {0} took {1}", CollectionId, time.Elapsed);

            foreach (var col in cols)
            {
                _writeTasks.Add(WriteToDisk(col.keyId, col.column));
            }
        }

        private async Task WriteToDisk(long keyId, VectorNode column)
        {
            var time = Stopwatch.StartNew();

            using (var columnWriter = new ColumnSerializer(
                CollectionId, keyId, SessionFactory, new RemotePostingsWriter(_config), "ixo", "ixop"))
            {
                await columnWriter.SerializeColumnSegment(column);
            }

            this.Log("serialized {0}.{1} in {2}", CollectionId, keyId, time.Elapsed);
        }

        private void Flush()
        {
            this.Log("flushing");

            var time = Stopwatch.StartNew();

            Task.WaitAll(_writeTasks.ToArray());

            var optimized = Directory.GetFiles(SessionFactory.Dir, string.Format("{0}.*.ixo", CollectionId));

            foreach (var file in optimized)
            {
                File.Replace(file, file.Replace(".ixo", ".ix"), file.Replace(".ixo", ".ixbak"));
                File.Replace(file.Replace(".ixo", ".ixop"), file.Replace(".ixo", ".ixp"), file.Replace(".ixo", ".ixpbak"));
            }

            this.Log("flushing took {0}", time.Elapsed);
        }

        public void Dispose()
        {
            _readSession.Dispose();

            Flush();
        }
    }
}