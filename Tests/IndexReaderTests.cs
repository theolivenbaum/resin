using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class IndexReaderTests
    {
        [Test]
        public void Can_read()
        {
            var dir = Setup.Dir + "\\indexread\\Can_read";
            var analyzer = new Analyzer();
            var parser = new QueryParser(analyzer);

            using (var w = new IndexWriter(dir, analyzer))
            {
                w.Write(new Dictionary<string, string>
                    {
                        {"title", "a"},
                         {"_id", "0"}
                    }
                );
                w.Write(new Dictionary<string, string>
                    {
                        {"title", "a b"},
                        {"_id", "1"}
                    }
                );
                w.Write(new Dictionary<string, string>
                    {
                        {"title", "a b c"},
                        {"_id", "2"}
                    }
                );
            }
            using (var reader = new IndexReader(dir))
            {
                var docs = reader.GetScoredResult(parser.Parse("title:a")).ToList();

                Assert.AreEqual(3, docs.Count);
                Assert.IsTrue(docs.Select(d => d.DocId).Contains("0"));
                Assert.IsTrue(docs.Select(d => d.DocId).Contains("1"));
                Assert.IsTrue(docs.Select(d => d.DocId).Contains("2"));

                docs = reader.GetScoredResult(parser.Parse("title:b")).ToList();

                Assert.AreEqual(2, docs.Count);
                Assert.IsFalse(docs.Select(d => d.DocId).Contains("0"));
                Assert.IsTrue(docs.Select(d => d.DocId).Contains("1"));
                Assert.IsTrue(docs.Select(d => d.DocId).Contains("2"));

                docs = reader.GetScoredResult(parser.Parse("title:c")).ToList();

                Assert.AreEqual(1, docs.Count);
                Assert.IsFalse(docs.Select(d => d.DocId).Contains("0"));
                Assert.IsFalse(docs.Select(d => d.DocId).Contains("1"));
                Assert.IsTrue(docs.Select(d => d.DocId).Contains("2"));

                docs = reader.GetScoredResult(parser.Parse("title:a +title:b")).ToList();

                Assert.AreEqual(2, docs.Count);
                Assert.IsFalse(docs.Select(d => d.DocId).Contains("0"));
                Assert.IsTrue(docs.Select(d => d.DocId).Contains("1"));
                Assert.IsTrue(docs.Select(d => d.DocId).Contains("2"));

                docs = reader.GetScoredResult(parser.Parse("title:a +title:b +title:c")).ToList();

                Assert.AreEqual(1, docs.Count);
                Assert.IsFalse(docs.Select(d => d.DocId).Contains("0"));
                Assert.IsFalse(docs.Select(d => d.DocId).Contains("1"));
                Assert.IsTrue(docs.Select(d => d.DocId).Contains("2"));
            }
        }
    }

    [TestFixture]
    public class TermTests
    {
        [Test]
        public void Can_calculate_distance_from_similarity()
        {
            Assert.AreEqual(6, new Term("title", "jrambo"){ Similarity = 0.009f}.Edits);
            Assert.AreEqual(5, new Term("title", "jrambo"){ Similarity = 0.2f}.Edits);
            Assert.AreEqual(4, new Term("title", "jrambo"){ Similarity = 0.3f}.Edits);
            Assert.AreEqual(3, new Term("title", "jrambo"){ Similarity = 0.5f}.Edits);
            Assert.AreEqual(2, new Term("title", "jrambo"){ Similarity = 0.7f}.Edits);
            Assert.AreEqual(1, new Term("title", "jrambo"){ Similarity = 0.8f}.Edits);
            Assert.AreEqual(1, new Term("title", "jrambo"){ Similarity = 0.9f}.Edits);
            Assert.AreEqual(0, new Term("title", "jrambo"){ Similarity = 0.999999f}.Edits);


            Assert.AreEqual(1, new Term("title", "abcde"){ Similarity = 0.8f}.Edits);
            Assert.AreEqual(1, new Term("title", "abcdef"){ Similarity = 0.8f}.Edits);
            Assert.AreEqual(1, new Term("title", "abcdefg"){ Similarity = 0.8f}.Edits);
            Assert.AreEqual(2, new Term("title", "abcdefgh"){ Similarity = 0.8f}.Edits);

            Assert.AreEqual(2, new Term("title", "abcde"){ Similarity = 0.7f}.Edits);
            Assert.AreEqual(2, new Term("title", "abcdef"){ Similarity = 0.7f}.Edits);
            Assert.AreEqual(2, new Term("title", "abcdefg"){ Similarity = 0.7f}.Edits);
            Assert.AreEqual(2, new Term("title", "abcdefgh") { Similarity = 0.7f }.Edits);
        }
    }
}