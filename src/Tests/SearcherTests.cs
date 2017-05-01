using NUnit.Framework;
using Resin;
using Resin.Analysis;
using Resin.IO;
using Resin.Querying;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tests
{
    public class SearcherTests
    {
        [Test]
        public void Can_search_exact()
        {
            var dir = Path.Combine(Setup.Dir, "Can_search_exact");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> {{"_id", "0"}, {"title", "Rambo First Blood"}},
                new Dictionary<string, string> {{"_id", "1"}, {"title", "rambo 2"}},
                new Dictionary<string, string> {{"_id", "2"}, {"title", "rocky 2"}},
                new Dictionary<string, string> {{"_id", "3"}, {"title", "the raiders of the lost ark"}},
                new Dictionary<string, string> {{"_id", "4"}, {"title", "the rain man"}},
                new Dictionary<string, string> {{"_id", "5"}, {"title", "the good, the bad and the ugly"}}
            };

            long indexName;
            using (var writer = new TestStreamUpsertOperation(dir, new Analyzer(), docs.ToStream()))
            {
                indexName = writer.Commit();
            }

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:rambo");

                Assert.That(result.Total, Is.EqualTo(2));
                Assert.That(result.Docs.Count, Is.EqualTo(2));

                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 0));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 1));

                Assert.AreEqual("Rambo First Blood", result.Docs.First(d => d.Document.Id == 0).Document.Fields["title"]);
            }

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:the");

                Assert.That(result.Total, Is.EqualTo(3));
                Assert.That(result.Docs.Count, Is.EqualTo(3));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 3));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 4));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 5));
            }
        }

    }
}