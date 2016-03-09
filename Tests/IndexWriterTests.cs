using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class IndexWriterTests
    {
        [Test]
        public void Can_overwrite_one_field()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_write_one_field";
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, string>
                    {
                        {"title", "Hello World!"}
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
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.idx").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);
        }

        [Test]
        public void Can_overwrite_two_fields()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_write_two_fields";
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
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.idx").Length);
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
                        {"title", "Goodbye Cruel World."},
                    }
                });
            }
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);
            using (var w = new IndexWriter(dir, new Analyzer(), overwrite:false))
            {
                w.Write(new Document
                {
                    Id = 2,
                    Fields = new Dictionary<string, string>
                    {
                        {"title", "The End"},
                    }
                });
            }
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
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
                    Fields = new Dictionary<string, string>
                    {
                        {"title", "Hello World!"},
                        {"body", "Once upon a time there was a man and a woman."}
                    }
                });
            }
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
            using (var w = new IndexWriter(dir, new Analyzer(), overwrite:false))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, string>
                    {
                        {"title", "Goodbye Cruel World."},
                        {"body", "Once upon a time there was a cat and a dog."}
                    }
                });
            }
            Assert.AreEqual(4, Directory.GetFiles(dir, "*.fld").Length);
        }
    }
}