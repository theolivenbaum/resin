using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Resin;
using Resin.IO;

namespace Tests
{
    [TestFixture]
    public class CollectorTests
    {
        [Test]
        public void Can_collect_exact()
        {
            var dir = Path.Combine(Setup.Dir, "Can_collect_exact");
            
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<IDictionary<string, string>>
            {
                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo"}},
                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
                new Dictionary<string, string> {{"_id", "3"}, {"title", "raiders of the lost ark"}},
                new Dictionary<string, string> {{"_id", "4"}, {"title", "rain man"}}
            };
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(docs);
            }

            var collector = new Collector(dir, IndexInfo.Load(Path.Combine(dir, "0.ix")));
            var postings = collector.Collect(new QueryContext("title", "rambo"), 0, 10, new Tfidf()).ToList();

            Assert.That(postings.Count, Is.EqualTo(2));
            Assert.IsTrue(postings.Any(d => d.DocId == "0"));
            Assert.IsTrue(postings.Any(d => d.DocId == "1"));
        }

        [Test]
        public void Can_collect_prefixed()
        {
            var dir = Path.Combine(Setup.Dir, "Can_collect_prefixed");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<IDictionary<string, string>>
            {
                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo"}},
                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
                new Dictionary<string, string> {{"_id", "3"}, {"title", "raiders of the lost ark"}},
                new Dictionary<string, string> {{"_id", "4"}, {"title", "rain man"}}
            };
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(docs);
            }

            var collector = new Collector(dir, IndexInfo.Load(Path.Combine(dir, "0.ix")));
            var postings = collector.Collect(new QueryContext("title", "ra") { Prefix = true }, 0, 10, new Tfidf()).ToList();

            Assert.That(postings.Count, Is.EqualTo(4));
            Assert.IsTrue(postings.Any(d => d.DocId == "0"));
            Assert.IsTrue(postings.Any(d => d.DocId == "1"));
            Assert.IsTrue(postings.Any(d => d.DocId == "3"));
            Assert.IsTrue(postings.Any(d => d.DocId == "4"));
        }

        [Test]
        public void Can_collect_near()
        {
            var dir = Path.Combine(Setup.Dir, "Can_collect_near");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<IDictionary<string, string>>
            {
                new Dictionary<string, string> {{"_id", "0"}, {"title", "rambo"}},
                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
                new Dictionary<string, string> {{"_id", "3"}, {"title", "raiders of the lost ark"}},
                new Dictionary<string, string> {{"_id", "4"}, {"title", "tomb raider"}}
            };
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(docs);
            }

            var collector = new Collector(dir, IndexInfo.Load(Path.Combine(dir, "0.ix")));
            var postings = collector.Collect(new QueryContext("title", "raider") { Fuzzy = true, Edits = 1 }, 0, 10, new Tfidf()).ToList();

            Assert.That(postings.Count, Is.EqualTo(2));
            Assert.IsTrue(postings.Any(d => d.DocId == "3"));
            Assert.IsTrue(postings.Any(d => d.DocId == "4"));
        }
    }
}