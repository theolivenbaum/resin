using Microsoft.VisualStudio.TestTools.UnitTesting;
using Resin;
using Resin.Analysis;
using Resin.IO;
using Resin.Sys;
using System.Collections.Generic;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class SearcherTests : Setup
    {
        [TestMethod]
        public void Can_search_compressed_index()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
            {
                new {_id = "0", title = "Rambo First Blood" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "the raiders of the lost ark" },
                new {_id = "4", title = "the rain man" },
                new {_id = "5", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertOperation(
                dir, new Analyzer(), compression: Compression.GZip, documents: docs);
            long indexName = writer.Write();
            writer.Dispose();

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:rambo");

                Assert.AreEqual(2, result.Total);
                Assert.AreEqual(2, result.Docs.Count);

                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 0));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 1));

                Assert.AreEqual(
                    "Rambo First Blood",
                    result.Docs.First(d => d.Document.Id == 0).Document.Fields["title"].Value);
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
            var dir = CreateDir();

            var docs = new List<dynamic>
{
                new {_id = "0", title = "Rambo First Blood" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "the raiders of the lost ark" },
                new {_id = "4", title = "the rain man" },
                new {_id = "5", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertOperation(
                dir, new Analyzer(), compression: Compression.NoCompression, documents: docs);
            long indexName = writer.Write();
            writer.Dispose();

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:rambo");

                Assert.AreEqual(2, result.Total);
                Assert.AreEqual(2, result.Docs.Count);

                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 0));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Id == 1));

                Assert.AreEqual(
                    "Rambo First Blood",
                    result.Docs.First(d => d.Document.Id == 0).Document.Fields["title"].Value);
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
        public void Can_return_query_terms()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
{
                new {_id = "0", title = "Rambo First Blood" },
                new {_id = "1", title = "rambo 2" },
                new {_id = "2", title = "rocky 2" },
                new {_id = "3", title = "the raiders of the lost ark" },
                new {_id = "4", title = "the rain man" },
                new {_id = "5", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertOperation(
                dir, new Analyzer(), compression: Compression.NoCompression, documents: docs);
            long indexName = writer.Write();
            writer.Dispose();

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:rambo blood title:first");

                Assert.AreEqual(3, result.QueryTerms.Length);

                Assert.IsTrue(result.QueryTerms.Any(d => d== "rambo"));
                Assert.IsTrue(result.QueryTerms.Any(d => d == "first"));
                Assert.IsTrue(result.QueryTerms.Any(d => d == "blood"));
            }
        }

        [TestMethod]
        public void Can_search_two_index_versions()
        {
            var dir = CreateDir();

            var docs = new List<dynamic>
{
                new {_id = "0", title = "Rambo First Blood" },
                new {_id = "1", title = "the rain man" },
                new {_id = "2", title = "the good, the bad and the ugly" }
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer = new UpsertOperation(
                dir, new Analyzer(), compression: Compression.NoCompression, documents: docs);
            long indexName = writer.Write();
            writer.Dispose();

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:rambo");

                Assert.AreEqual(1, result.Total);
                Assert.AreEqual(1, result.Docs.Count);

                Assert.IsTrue(result.Docs.Any(d => d.Document.Fields["_id"].Value == "0"));

            }

            var moreDocs = new List<dynamic>
{
                new {_id = "3", title = "rambo 2" },
                new {_id = "4", title = "rocky 2" },
                new {_id = "5", title = "the raiders of the lost ark" },
            }.ToDocuments(primaryKeyFieldName: "_id");

            var writer2 = new UpsertOperation(
                dir, new Analyzer(), compression: Compression.NoCompression, documents: moreDocs);
            long indexName2 = writer2.Write();
            writer2.Dispose();

            using (var searcher = new Searcher(dir))
            {
                var result = searcher.Search("title:rambo");

                Assert.AreEqual(2, result.Total);
                Assert.AreEqual(2, result.Docs.Count);

                Assert.IsTrue(result.Docs.Any(d => d.Document.Fields["_id"].Value == "0"));
                Assert.IsTrue(result.Docs.Any(d => d.Document.Fields["_id"].Value == "3"));

            }
        }

    }
}