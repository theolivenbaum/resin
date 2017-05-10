using Microsoft.VisualStudio.TestTools.UnitTesting;
using Resin;
using Resin.Analysis;
using Resin.IO;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class SearcherTests : Setup
    {
        [TestMethod]
        public void Can_search_compressed_index()
        {
            var dir = Path.Combine(Dir, "SearcherTests.Can_search_compressed_index");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<Field>
            {
                new Field(0, "_id", "0"), new Field(0, "title", "Rambo First Blood"),
                new Field(1, "_id", "1"), new Field(1, "title", "rambo 2"),
                new Field(2, "_id", "2"), new Field(2, "title", "rocky 2"),
                new Field(3, "_id", "3"), new Field(3, "title", "the raiders of the lost ark"),
                new Field(4, "_id", "4"), new Field(4, "title", "the rain man"),
                new Field(5, "_id", "5"), new Field(5, "title", "the good, the bad and the ugly")
            }.GroupBy(f => f.DocumentId).Select(g => new Document(g.Key, g.ToList()));

            var writer = new DocumentUpsertOperation(dir, new Analyzer(), compression: Compression.GZip, primaryKey: "_id", documents: docs);
            long indexName = writer.Commit();

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:rambo");

                Assert.AreEqual(2, result.Total);
                Assert.AreEqual(2, result.Docs.Count);

                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 0));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 1));

                Assert.AreEqual(
                    "Rambo First Blood",
                    result.Docs.First(d => d.Document.Id == 0).Document.Fields.First(f => f.Key == "title").Value);
            }

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:the");

                Assert.AreEqual(3, result.Total);
                Assert.AreEqual(3, result.Docs.Count);
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 3));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 4));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 5));
            }
        }

        [TestMethod]
        public void Can_search_exact()
        {
            var dir = Path.Combine(Dir, "SearcherTests.Can_search_exact");

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var docs = new List<Field>
            {
                new Field(0, "_id", "0"), new Field(0, "title", "Rambo First Blood"),
                new Field(1, "_id", "1"), new Field(1, "title", "rambo 2"),
                new Field(2, "_id", "2"), new Field(2, "title", "rocky 2"),
                new Field(3, "_id", "3"), new Field(3, "title", "the raiders of the lost ark"),
                new Field(4, "_id", "4"), new Field(4, "title", "the rain man"),
                new Field(5, "_id", "5"), new Field(5, "title", "the good, the bad and the ugly")
            }.GroupBy(f => f.DocumentId).Select(g => new Document(g.Key, g.ToList()));

            var writer = new DocumentUpsertOperation(dir, new Analyzer(), compression: Compression.NoCompression, primaryKey: "_id", documents: docs);
            long indexName = writer.Commit();

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:rambo");

                Assert.AreEqual(2, result.Total);
                Assert.AreEqual(2, result.Docs.Count);

                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 0));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 1));

                Assert.AreEqual(
                    "Rambo First Blood", 
                    result.Docs.First(d => d.Document.Id == 0).Document.Fields.First(f=>f.Key == "title").Value);
            }

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:the");

                Assert.AreEqual(3, result.Total);
                Assert.AreEqual(3, result.Docs.Count);
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 3));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 4));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 5));
            }
        }

    }
}