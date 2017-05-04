//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using NUnit.Framework;
//using Resin;
//using Resin.Analysis;
//using Resin.IO;
//using Resin.Querying;

//namespace Tests
//{
//    [TestFixture]
//    public class CollectorTests
//    {
//        [Test]
//        public void Can_collect_by_id()
//        {
//            var dir = Path.Combine(Setup.Dir, "Can_collect_by_id");

//            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

//            var docs = new List<Dictionary<string, string>>
//            {
//                new Dictionary<string, string> {{"_id", "abc0123"}, {"title", "rambo first blood"}},
//                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
//                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
//                new Dictionary<string, string> {{"_id", "3"}, {"title", "the raiders of the lost ark"}},
//                new Dictionary<string, string> {{"_id", "four"}, {"title", "the rain man"}},
//                new Dictionary<string, string> {{"_id", "5five"}, {"title", "the good, the bad and the ugly"}}
//            };

//            var writer = new DocumentUpsertOperation(dir, new Analyzer(), compression: true, primaryKey: "_id", documents: docs);
//            long indexName = writer.Commit();

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName + ".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(new QueryContext("_id", "3")).ToList();

//                Assert.That(scores.Count, Is.EqualTo(1));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
//            }

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(new QueryContext("_id", "5five")).ToList();

//                Assert.That(scores.Count, Is.EqualTo(1));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
//            }
//        }

//        [Test]
//        public void Can_collect_near_phrase()
//        {
//            var dir = Path.Combine(Setup.Dir, "Can_collect_near_phrase_joined_by_and");

//            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

//            var docs = new List<Dictionary<string, string>>
//            {
//                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo first blood"}},
//                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
//                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
//                new Dictionary<string, string> {{"_id", "3"}, {"title", "the raid"}},
//                new Dictionary<string, string> {{"_id", "4"}, {"title", "the rain man"}},
//                new Dictionary<string, string> {{"_id", "5"}, {"title", "the good, the bad and the ugly"}}
//            };

//            var writer = new DocumentUpsertOperation(dir, new Analyzer(), compression: true, primaryKey: "_id", documents: docs);
//            long indexName = writer.Commit();

//            var query = new QueryParser(new Analyzer()).Parse("+title:rain man");

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName + ".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(query).ToList();

//                Assert.That(scores.Count, Is.EqualTo(1));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
//            }

//            query = new QueryParser(new Analyzer(), 0.75f).Parse("+title:rain man~");

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(query).ToList();

//                Assert.That(scores.Count, Is.EqualTo(1));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
//            }
//        }

//        [Test]
//        public void Can_collect_exact_phrase_joined_by_and()
//        {
//            var dir = Path.Combine(Setup.Dir, "Can_collect_exact_phrase_joined_by_and");

//            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

//            var docs = new List<Dictionary<string, string>>
//            {
//                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo first blood"}},
//                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
//                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
//                new Dictionary<string, string> {{"_id", "3"}, {"title", "the raiders of the lost ark"}},
//                new Dictionary<string, string> {{"_id", "4"}, {"title", "the rain man"}},
//                new Dictionary<string, string> {{"_id", "5"}, {"title", "the good, the bad and the ugly"}}
//            };

//            var writer = new DocumentUpsertOperation(dir, new Analyzer(), compression: true, primaryKey: "_id", documents: docs);
//            long indexName = writer.Commit();

//            var query = new QueryParser(new Analyzer()).Parse("+title:the");

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(query).ToList();

//                Assert.That(scores.Count, Is.EqualTo(3));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
//            }

//            query = new QueryParser(new Analyzer()).Parse("+title:the +title:ugly");

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(query).ToList();

//                Assert.That(scores.Count, Is.EqualTo(1));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
//            }
//        }

//        [Test]
//        public void Can_collect_exact_phrase_joined_by_or()
//        {
//            var dir = Path.Combine(Setup.Dir, "Can_collect_exact_phrase_joined_by_or");

//            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

//            var docs = new List<Dictionary<string, string>>
//            {
//                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo first blood"}},
//                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
//                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
//                new Dictionary<string, string> {{"_id", "3"}, {"title", "the raiders of the lost ark"}},
//                new Dictionary<string, string> {{"_id", "4"}, {"title", "the rain man"}},
//                new Dictionary<string, string> {{"_id", "5"}, {"title", "the good, the bad and the ugly"}}
//            };

//            var writer = new DocumentUpsertOperation(dir, new Analyzer(), compression: true, primaryKey: "_id", documents: docs);
//            long indexName = writer.Commit();

//            var query = new QueryParser(new Analyzer()).Parse("+title:rocky");

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(query).ToList();

//                Assert.That(scores.Count, Is.EqualTo(1));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 2));
//            }

//            query = new QueryParser(new Analyzer()).Parse("+title:rambo");

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(query).ToList();

//                Assert.That(scores.Count, Is.EqualTo(2));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 0));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
//            }

//            query = new QueryParser(new Analyzer()).Parse("+title:rocky title:rambo");

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(query).ToList();

//                Assert.That(scores.Count, Is.EqualTo(3));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 0));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 2));
//            }
//        }

//        [Test]
//        public void Can_collect_exact_phrase_joined_by_not()
//        {
//            var dir = Path.Combine(Setup.Dir, "Can_collect_exact_phrase_joined_by_not");

//            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

//            var docs = new List<Dictionary<string, string>>
//            {
//                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo first blood"}},
//                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
//                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
//                new Dictionary<string, string> {{"_id", "3"}, {"title", "the raiders of the lost ark"}},
//                new Dictionary<string, string> {{"_id", "4"}, {"title", "the rain man"}},
//                new Dictionary<string, string> {{"_id", "5"}, {"title", "the good, the bad and the ugly"}}
//            };

//            var writer = new DocumentUpsertOperation(dir, new Analyzer(), compression: true, primaryKey: "_id", documents: docs);
//            long indexName = writer.Commit();

//            var query = new QueryParser(new Analyzer()).Parse("+title:the");

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(query).ToList();

//                Assert.That(scores.Count, Is.EqualTo(3));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
//            }

//            query = new QueryParser(new Analyzer()).Parse("+title:the -title:ugly");

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(query).ToList();

//                Assert.That(scores.Count, Is.EqualTo(2));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
//            }
//        }

//        [Test]
//        public void Can_collect_exact()
//        {
//            var dir = Path.Combine(Setup.Dir, "Can_collect_exact");
            
//            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

//            var docs = new List<Dictionary<string, string>>
//            {
//                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo first blood"}},
//                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
//                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
//                new Dictionary<string, string> {{"_id", "3"}, {"title", "the raiders of the lost ark"}},
//                new Dictionary<string, string> {{"_id", "4"}, {"title", "the rain man"}},
//                new Dictionary<string, string> {{"_id", "5"}, {"title", "the good, the bad and the ugly"}}
//            };

//            var writer = new DocumentUpsertOperation(dir, new Analyzer(), compression: true, primaryKey: "_id", documents: docs);
//            long indexName = writer.Commit();

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(new QueryContext("title", "rambo")).ToList();

//                Assert.That(scores.Count, Is.EqualTo(2));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 0));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));  
//            }

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(new QueryContext("title", "the")).ToList();

//                Assert.That(scores.Count, Is.EqualTo(3));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 5));
//            }
//        }

//        [Test]
//        public void Can_collect_prefixed()
//        {
//            var dir = Path.Combine(Setup.Dir, "Can_collect_prefixed");

//            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

//            var docs = new List<Dictionary<string, string>>
//            {
//                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo"}},
//                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
//                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
//                new Dictionary<string, string> {{"_id", "3"}, {"title", "raiders of the lost ark"}},
//                new Dictionary<string, string> {{"_id", "4"}, {"title", "rain man"}}
//            };

//            var writer = new DocumentUpsertOperation(dir, new Analyzer(), compression: true, primaryKey: "_id", documents: docs);
//            long indexName = writer.Commit();

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(new QueryContext("title", "ra") { Prefix = true }).ToList();

//                Assert.That(scores.Count, Is.EqualTo(4));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 0));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 1));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
//            }
//        }

//        [Test]
//        public void Can_collect_near()
//        {
//            var dir = Path.Combine(Setup.Dir, "Can_collect_near");

//            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

//            var docs = new List<Dictionary<string, string>>
//            {
//                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo"}},
//                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
//                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
//                new Dictionary<string, string> {{"_id", "3"}, {"title", "raiders of the lost ark"}},
//                new Dictionary<string, string> {{"_id", "4"}, {"title", "tomb raider"}}
//            };

//            var writer = new DocumentUpsertOperation(dir, new Analyzer(), compression: true, primaryKey: "_id", documents: docs);
//            long indexName = writer.Commit();

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName + ".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(new QueryContext("title", "raider") { Fuzzy = false, Edits = 1 }).ToList();

//                Assert.That(scores.Count, Is.EqualTo(1));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
//            }

//            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, indexName+".ix")), new Tfidf()))
//            {
//                var scores = collector.Collect(new QueryContext("title", "raider") { Fuzzy = true, Edits = 1 }).ToList();

//                Assert.That(scores.Count, Is.EqualTo(2));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 3));
//                Assert.IsTrue(scores.Any(d => d.DocumentId == 4));
//            }
//        }
//    }
//}