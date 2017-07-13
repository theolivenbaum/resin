using System.Collections.Generic;
using System.IO;
using System.Linq;
using Resin;
using Resin.Analysis;
using Resin.Querying;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DocumentTable;
using System;

namespace Tests
{
    [TestClass]
    public class CollectorTests : Setup
    {
        [TestMethod]
        public void Can_collect_by_id()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
            {
                new {_id = "abc0123", title = "rambo first blood" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "the raiders of the lost ark" },
                new {_id = "four", title = "the rain man" },
                new {_id = "5five", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            long version;
            using (var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs))
            {
                version = writer.Write();
            }
            using(var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(new QueryContext("_id", "3")).ToList();

                Assert.AreEqual(1, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
            }

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(new QueryContext("_id", "5five")).ToList();

                Assert.AreEqual(1, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
            }
        }

        [TestMethod]
        public void Can_collect_near_phrase()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
            {
                new {_id = "0", title = "rambo first blood" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "the raid" },
                new {_id = "4", title = "the rain man" },
                new {_id = "5", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs);
            long version = writer.Write();
            writer.Dispose();

            var query = new QueryParser(new Analyzer()).Parse("+title:rain man");

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(1, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
            }

            query = new QueryParser(new Analyzer(), 0.75f).Parse("+title:rain man~");

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(2, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
            }
        }

        [TestMethod]
        public void Can_collect_exact_phrase_joined_by_and()
        {
            var dir = CreateDir();

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<dynamic>
            {
                new {_id = "0", title = "rambo first blood" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "raiders of the lost ark" },
                new {_id = "4", title = "the rain man" },
                new {_id = "5", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs);
            long version = writer.Write();
            writer.Dispose();

            var query = new QueryParser(new Analyzer()).Parse("+title:the");

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(3, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
            }

            query = new QueryParser(new Analyzer()).Parse("+title:the +title:ugly");

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(1, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
            }
        }

        [TestMethod]
        public void Can_collect_exact_phrase_joined_by_or()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
            {
                new {_id = "0", title = "rambo first blood" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "raiders of the lost ark" },
                new {_id = "4", title = "the rain man" },
                new {_id = "5", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs);
            long version = writer.Write();
            writer.Dispose();

            var query = new QueryParser(new Analyzer()).Parse("+title:rocky");

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(1, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 2));
            }

            query = new QueryParser(new Analyzer()).Parse("+title:rambo");

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(2, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 0));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
            }

            query = new QueryParser(new Analyzer()).Parse("+title:rocky title:rambo");

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(3, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 0));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 2));
            }
        }

        [TestMethod]
        public void Can_collect_exact_phrase_joined_by_not()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
            {
                new {_id = "0", title = "rambo first blood" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "raiders of the lost ark" },
                new {_id = "4", title = "the rain man" },
                new {_id = "5", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs);
            long version = writer.Write();
            writer.Dispose();

            var query = new QueryParser(new Analyzer()).Parse("+title:the");

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(3, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
            }

            query = new QueryParser(new Analyzer()).Parse("+title:the -title:ugly");

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(2, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
            }
        }

        [TestMethod]
        public void Can_collect_exact()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
            {
                new {_id = "0", title = "rambo first blood" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "raiders of the lost ark" },
                new {_id = "4", title = "the rain man" },
                new {_id = "5", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs);
            long version = writer.Write();
            writer.Dispose();

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(new QueryContext("title", "rambo")).ToList();

                Assert.AreEqual(2, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 0));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
            }

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(new QueryContext("title", "the")).ToList();

                Assert.AreEqual(3, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
            }
        }

        [TestMethod]
        public void Can_delete()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
            {
                new {_id = "0", title = "rambo first blood" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "raiders of the lost ark" },
                new {_id = "4", title = "the rain man" },
                new {_id = "5", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs);
            long version = writer.Write();
            writer.Dispose();

            using(var factory = new ReadSessionFactory(dir))
            using (var readSession = factory.OpenReadSession(version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(new QueryContext("title", "rambo")).ToList();

                Assert.AreEqual(2, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 0));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
            }

            var operation = new DeleteByPrimaryKeyCommand(dir, new[] { "0" });
            operation.Execute();

            using (var factory = new ReadSessionFactory(dir))
            using (var readSession = factory.OpenReadSession(version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(new QueryContext("title", "rambo")).ToList();

                Assert.AreEqual(1, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
            }
        }

        [TestMethod]
        public void Can_collect_prefixed()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
            {
                new {_id = "0", title = "rambo" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "raiders of the lost ark" },
                new {_id = "4", title = "rain man" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs);
            long version = writer.Write();
            writer.Dispose();

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(new QueryContext("title", "ra") { Prefix = true }).ToList();

                Assert.AreEqual(4, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 0));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
            }
        }

        [TestMethod]
        public void Can_collect_near()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
            {
                new {_id = "0", title = "rambo" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "raiders of the lost ark" },
                new {_id = "4", title = "tomb raider" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs);
            long version = writer.Write();
            writer.Dispose();

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(new QueryContext("title", "raider") { Fuzzy = false, Edits = 1 }).ToList();

                Assert.AreEqual(1, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
            }

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(new QueryContext("title", "raider") { Fuzzy = true, Edits = 1 }).ToList();

                Assert.AreEqual(2, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
            }
        }

        [TestMethod]
        public void Can_collect_numbers()
        {
            var dir = CreateDir();

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<dynamic>
            {
                new {_id = "0", title = 5 },
                new {_id = "1", title = 4 },
                new {_id = "2", title = 3 },
                new {_id = "3", title = 2 },
                new {_id = "4", title = 1 },
                new {_id = "5", title = 0 }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs);
            long version = writer.Write();
            writer.Dispose();

            var query = new QueryContext("title", 3);

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(1, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 2));
            }

            query = new QueryContext("title", 0, 3);

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(4, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 2));
            }
        }

        [TestMethod]
        public void Can_collect_dates()
        {
            var dir = CreateDir();

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var lowerBound = DateTime.Now;
            var upperBound = DateTime.Now.AddDays(1);

            var docs = new List<dynamic>
            {
                new {_id = "0", created = DateTime.Now.AddDays(-1) },
                new {_id = "1", created = lowerBound  },
                new {_id = "2", created = upperBound  },
                new {_id = "3", created = upperBound.AddDays(1)  },
                new {_id = "4", created = upperBound.AddDays(2)  },
                new {_id = "5", created = upperBound.AddDays(3)  }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertTransaction(dir, new Analyzer(), compression: Compression.Lz, documents: docs);
            long version = writer.Write();
            writer.Dispose();

            var query = new QueryContext("created", upperBound);

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(1, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 2));
            }

            query = new QueryContext("created", lowerBound);

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(1, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
            }

            query = new QueryContext("created", lowerBound, upperBound);

            using (var readSession = CreateReadSession(dir, version))
            using (var collector = new Collector(dir, readSession))
            {
                var scores = collector.Collect(query).ToList();

                Assert.AreEqual(2, scores.Count);
                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
                Assert.IsTrue(scores.Any(d => d.DocumentId == 2));
            }
        }
    }
}