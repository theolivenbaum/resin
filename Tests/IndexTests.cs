using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class IndexTests
    {
        [Test]
        public void Can_find()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_find";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            var analyzer = new Analyzer();
            var parser = new QueryParser(analyzer);

            using (var w = new IndexWriter(dir, analyzer))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new[]{"a"}.ToList()}
                    }
                });
                w.Write(new Document
                {
                    Id = 1,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new[]{"a b"}.ToList()}
                    }
                });
                w.Write(new Document
                {
                    Id = 2,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new[]{"a b c"}.ToList()}
                    }
                });
            }
            using (var reader = new IndexReader(new Scanner(dir)))
            {
                var docs = reader.GetDocuments("title", "a").ToList();

                Assert.AreEqual(3, docs.Count);

                docs = reader.GetDocuments("title", "b").ToList();

                Assert.AreEqual(2, docs.Count);

                docs = reader.GetDocuments("title", "c").ToList();

                Assert.AreEqual(1, docs.Count);

                docs = reader.GetDocuments(parser.Parse("title:a +title:b").ToList()).ToList();

                Assert.AreEqual(2, docs.Count);

                docs = reader.GetDocuments(parser.Parse("title:a +title:b +title:c").ToList()).ToList();

                Assert.AreEqual(1, docs.Count);
            }
        }

        [Test]
        public void Can_write_one_field()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_write_one_field";
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Hello World!"}.ToList()}
                    }
                });
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Goodbye Cruel World."}.ToList()}
                    }
                });
            }

            Assert.AreEqual(1, Directory.GetFiles(dir, "fld.ix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "d.ix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);
        }

        [Test]
        public void Can_write_two_fields()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_write_two_fields";
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Hello World!"}.ToList()},
                        {"body", new []{"Once upon a time there was a man and a woman."}.ToList()}
                    }
                });
            }
            Assert.AreEqual(1, Directory.GetFiles(dir, "fld.ix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "d.ix").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
        }

        

        [Test]
        public void Can_append_to_one_field()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_append_one_field";
            if(Directory.Exists(dir)) Directory.Delete(dir, true);

            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Hello World!"}.ToList()},
                    }
                });
            }

            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);

            using (var reader = new IndexReader(new Scanner(dir)))
            {
                var terms = reader.Scanner.GetAllTokens("title").Select(t => t.Token).ToList();

                Assert.AreEqual(2, terms.Count);
                Assert.IsTrue(terms.Contains("hello"));
                Assert.IsTrue(terms.Contains("world"));

                var docs = reader.GetDocuments("title", "world").ToList();

                Assert.AreEqual(1, docs.Count);
                Assert.AreEqual("Hello World!", docs[0].Fields["title"][0]);

                docs = reader.GetDocuments("title", "cruel").ToList();

                Assert.AreEqual(0, docs.Count);
            }

            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 1,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Hello Cruel World!"}.ToList()},
                    }
                });
            }

            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);

            using (var reader = new IndexReader(new Scanner(dir)))
            {
                var terms = reader.Scanner.GetAllTokens("title").Select(t => t.Token).ToList();

                Assert.AreEqual(3, terms.Count);
                Assert.IsTrue(terms.Contains("hello"));
                Assert.IsTrue(terms.Contains("world"));
                Assert.IsTrue(terms.Contains("cruel"));

                var docs = reader.GetDocuments("title", "world").ToList();

                Assert.AreEqual(2, docs.Count);
                Assert.AreEqual("Hello World!", docs[0].Fields["title"][0]);
                Assert.AreEqual("Hello Cruel World!", docs[1].Fields["title"][0]);

                docs = reader.GetDocuments("title", "cruel").ToList();

                Assert.AreEqual(1, docs.Count);
                Assert.AreEqual("Hello Cruel World!", docs[0].Fields["title"][0]);
            }
        }

        [Test]
        public void Can_append_to_two_fields()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_append_two_fields";
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Hello World!"}.ToList()},
                        {"body", new []{"Once upon a time there was a man and a woman."}.ToList()}
                    }
                });
            }
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Goodbye Cruel World."}.ToList()},
                        {"body", new []{"Once upon a time there was a cat and a dog."}.ToList()}
                    }
                });
            }
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
        }
    }
}