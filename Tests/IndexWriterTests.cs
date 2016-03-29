using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class IndexWriterTests
    {
        [Test]
        public void Can_update()
        {
            const string dir = Setup.Dir + "\\Can_update";
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, string>
                    {
                        {"title","hello"}
                    }
                });
            }
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, string>
                    {
                        {"title","hello"}
                    }
                });
            }
            Assert.AreEqual(12, Directory.GetFiles(dir).Length);

            Assert.AreEqual(2, Directory.GetFiles(dir, "*.ix").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.dix").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fix").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.f").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.f.tri").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.d").Length);
        }

        [Test]
        public void Can_write_one_field()
        {
            const string dir = Setup.Dir + "\\Can_write_one_field";
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, string>
                    {
                        {"title","Hello World!"}
                    }
                });
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, string>
                    {
                        {"title", "Goodbye Cruel World."}
                    }
                });
            }
            Assert.AreEqual(6, Directory.GetFiles(dir).Length);

            Assert.AreEqual(1, Directory.GetFiles(dir, "*.ix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.dix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.f").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.f.tri").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.d").Length);
        }

        [Test]
        public void Can_write_two_fields()
        {
            const string dir = Setup.Dir + "\\Can_write_two_fields";
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, string>
                    {
                        {"title", "Hello World!"},
                        {"body", "Once upon a time there was a man and a woman."}
                    }
                });
            }
            Assert.AreEqual(8, Directory.GetFiles(dir).Length);

            Assert.AreEqual(1, Directory.GetFiles(dir, "*.ix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.dix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fix").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.f").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.f.tri").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.d").Length);
        }

        [Test]
        public void Can_overwrite_doc()
        {
            var dir = Setup.Dir + "\\Can_overwrite_doc";
            //var fileName0 = dir + "\\0.fld";
            //var fileName1 = dir + "\\1.fld";
            //var fileName2 = dir + "\\2.fld";

            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, string>
                    {
                        {"title", "Hello World!"},
                    }
                });

                w.Write(new Document
                {
                    Id = 1,
                    Fields = new Dictionary<string, string>
                    {
                        {"title", "Hello Cruel World!"},
                    }
                });
            }

            var parser = new QueryParser(new Analyzer());
            using (var reader = new IndexReader(dir))
            {
                var terms = reader.FieldScanner.GetAllTokens("title").Select(t => t.Token).ToList();

                Assert.AreEqual(3, terms.Count);
                Assert.IsTrue(terms.Contains("hello"));
                Assert.IsTrue(terms.Contains("world"));
                Assert.IsTrue(terms.Contains("cruel"));
                Assert.IsFalse(terms.Contains("mighty"));

                var docs = reader.GetScoredResult(parser.Parse("title:world")).ToList();

                Assert.AreEqual(2, docs.Count);

                docs = reader.GetScoredResult(parser.Parse("title:cruel")).ToList();

                Assert.AreEqual(1, docs.Count);
            }

            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, string>
                    {
                        {"title", "Hello mighty Cruel, cruel World!"},
                    }
                });
            }
            using (var reader = new IndexReader(dir))
            {
                var terms = reader.FieldScanner.GetAllTokens("title").ToList();

                Assert.AreEqual(4, terms.Count);

                Assert.IsNotNull(terms.FirstOrDefault(t=>t.Token == "hello"));
                Assert.IsNotNull(terms.FirstOrDefault(t => t.Token == "world"));
                Assert.IsNotNull(terms.FirstOrDefault(t => t.Token == "cruel"));
                Assert.IsNotNull(terms.FirstOrDefault(t => t.Token == "mighty"));

                Assert.AreEqual(2, terms.First(t => t.Token == "hello").Count);
                Assert.AreEqual(2, terms.First(t => t.Token == "world").Count);
                Assert.AreEqual(3, terms.First(t => t.Token == "cruel").Count);
                Assert.AreEqual(1, terms.First(t => t.Token == "mighty").Count);

                var docs = reader.GetScoredResult(parser.Parse("title:world")).ToList();

                Assert.AreEqual(2, docs.Count);

                docs = reader.GetScoredResult(parser.Parse("title:cruel")).ToList();

                Assert.AreEqual(2, docs.Count);
            }
        }
    }
}