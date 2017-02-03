using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Resin;
using Resin.Analysis;
using Resin.IO;
using Resin.Querying;

namespace Tests
{
    [TestFixture]
    public class CollectorTests
    {
        [Test]
        public void Can_rank_fuzzy_phrase()
        {
            var dir = Path.Combine(Setup.Dir, "Can_rank_fuzzy_phrase");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> {{"_id", "0"}, {"title", "Tage Mage"}},
                new Dictionary<string, string> {{"_id", "1"}, {"title", "aye-aye"}},
                new Dictionary<string, string> {{"_id", "2"}, {"title", "Cage Rage Championships"}},
                new Dictionary<string, string> {{"_id", "3"}, {"title", "Page Up and Page Down keys"}},
                new Dictionary<string, string> {{"_id", "4"}, {"title", "Golden Age of Porn"}}
            };
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(docs.Select(d => new Document(d)));
            }

            var query = new QueryParser(new Analyzer()).Parse("+title:age of porn~");

            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, "0.ix")), new Tfidf()))
            {
                var scores = collector.Collect(query).ToList();

                Assert.That(scores.Count, Is.EqualTo(5));
                Assert.IsTrue(scores.First().DocId.Equals("4"));
            }
        }

        [Test]
        public void Can_rank_fuzzy_term()
        {
            var dir = Path.Combine(Setup.Dir, "Can_rank_fuzzy_term");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> {{"_id", "0"}, {"title", "Gustav Horn, Count of Pori"}},
                new Dictionary<string, string> {{"_id", "1"}, {"title", "Port au Port Peninsula"}},
                new Dictionary<string, string> {{"_id", "2"}, {"title", "Pore"}},
                new Dictionary<string, string> {{"_id", "3"}, {"title", "Porn 2.0"}},
                new Dictionary<string, string> {{"_id", "4"}, {"title", "Porn"}}
            };
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(docs.Select(d => new Document(d)));
            }

            var query = new QueryParser(new Analyzer()).Parse("+title:porn~");

            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, "0.ix")), new Tfidf()))
            {
                var scores = collector.Collect(query).ToList();

                Assert.That(scores.Count, Is.EqualTo(5));
                Assert.IsTrue(scores.First().DocId.Equals("4"));
            }
        }

        [Test]
        public void Can_collect_phrase()
        {
            var dir = Path.Combine(Setup.Dir, "Can_collect_phrase");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo"}},
                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
                new Dictionary<string, string> {{"_id", "3"}, {"title", "raiders of the lost ark"}},
                new Dictionary<string, string> {{"_id", "4"}, {"title", "rain man"}}
            };
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(docs.Select(d => new Document(d)));
            }

            var query = new QueryParser(new Analyzer()).Parse("+title:the lost ark");

            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, "0.ix")), new Tfidf()))
            {
                var scores = collector.Collect(query).ToList();

                Assert.That(scores.Count, Is.EqualTo(1));
                Assert.IsTrue(scores.Any(d => d.DocId == "3"));
            }
        }

        [Test]
        public void Can_collect_exact()
        {
            var dir = Path.Combine(Setup.Dir, "Can_collect_exact");
            
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo"}},
                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
                new Dictionary<string, string> {{"_id", "3"}, {"title", "raiders of the lost ark"}},
                new Dictionary<string, string> {{"_id", "4"}, {"title", "rain man"}}
            };
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(docs.Select(d=>new Document(d)));
            }

            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, "0.ix")), new Tfidf()))
            {
                var scores = collector.Collect(new QueryContext("title", "rambo")).ToList();

                Assert.That(scores.Count, Is.EqualTo(2));
                Assert.IsTrue(scores.Any(d => d.DocId == "0"));
                Assert.IsTrue(scores.Any(d => d.DocId == "1"));  
            }
        }

        [Test]
        public void Can_collect_prefixed()
        {
            var dir = Path.Combine(Setup.Dir, "Can_collect_prefixed");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo"}},
                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
                new Dictionary<string, string> {{"_id", "3"}, {"title", "raiders of the lost ark"}},
                new Dictionary<string, string> {{"_id", "4"}, {"title", "rain man"}}
            };
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(docs.Select(d => new Document(d)));
            }

            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, "0.ix")), new Tfidf()))
            {
                var scores = collector.Collect(new QueryContext("title", "ra") { Prefix = true }).ToList();

                Assert.That(scores.Count, Is.EqualTo(4));
                Assert.IsTrue(scores.Any(d => d.DocId == "0"));
                Assert.IsTrue(scores.Any(d => d.DocId == "1"));
                Assert.IsTrue(scores.Any(d => d.DocId == "3"));
                Assert.IsTrue(scores.Any(d => d.DocId == "4"));
            }
        }

        [Test]
        public void Can_collect_near()
        {
            var dir = Path.Combine(Setup.Dir, "Can_collect_near");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo"}},
                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
                new Dictionary<string, string> {{"_id", "3"}, {"title", "raiders of the lost ark"}},
                new Dictionary<string, string> {{"_id", "4"}, {"title", "tomb raider"}}
            };
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(docs.Select(d => new Document(d)));
            }

            using (var collector = new Collector(dir, IxInfo.Load(Path.Combine(dir, "0.ix")), new Tfidf()))
            {
                var scores = collector.Collect(new QueryContext("title", "raider") { Fuzzy = true, Edits = 1 }).ToList();

                Assert.That(scores.Count, Is.EqualTo(2));
                Assert.IsTrue(scores.Any(d => d.DocId == "3"));
                Assert.IsTrue(scores.Any(d => d.DocId == "4"));
            }
        }
    }
}