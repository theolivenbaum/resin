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
                w.Write(new Document
                {
                    Id = 1,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"Goodbye Cruel World."}.ToList()},
                    }
                });
            }
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 2,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new []{"The End"}.ToList()},
                    }
                });
            }
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "fld.ix").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "d.ix").Length);
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