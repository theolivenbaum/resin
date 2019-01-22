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
        
        public OptimizeSession(
            string collectionId,
            SessionFactory sessionFactory,
            IConfigurationProvider config) : base(collectionId, sessionFactory)
        {
            _config = config;
            _readSession = new ReadSession(collectionId, sessionFactory, config);
            _writeTasks = new List<Task>();
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

                        // TODO: instead of each node having a postings offset,
                        // let them have a list of posting offsets.
                        // Because this will not work otherwise:
                        optimized.Add(node);
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

            using (var columnWriter = new ColumnWriter(CollectionId, keyId, SessionFactory, null, "ixo"))
            {
                await columnWriter.WriteColumnSegment(column);
            }

            this.Log("serialized {0}.{1} in {2}", CollectionId, keyId, time.Elapsed);
        }

        private void Flush()
        {
            this.Log("flushing");

            var time = Stopwatch.StartNew();

            Task.WaitAll(_writeTasks.ToArray());

            this.Log("flushing took {0}", time.Elapsed);
        }

        public void Dispose()
        {
            _readSession.Dispose();

            Flush();
        }
    }
}