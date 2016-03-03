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
            using (var iw = new IndexWriter(dir, new Analyzer()))
            {
                iw.Add(0, "title", "Hello World!");
                iw.Add(1, "title", "Goodbye Cruel World.");
            }
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.idx").Length);
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);
        }

        [Test]
        public void Can_overwrite_two_fields()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_write_two_fields";
            using (var iw = new IndexWriter(dir, new Analyzer()))
            {
                iw.Add(0, "title", "Hello World!");
                iw.Add(0, "body", "Once upon a time there was a man and a woman.");
            }
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.idx").Length);
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
        }

        [Test]
        public void Can_append_to_one_field()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_append_one_field";
            using (var iw = new IndexWriter(dir, new Analyzer()))
            {
                iw.Add(0, "title", "Hello World!");
                iw.Add(1, "title", "Goodbye Cruel World.");
            }
            Assert.AreEqual(1, Directory.GetFiles(dir, "*.fld").Length);
            using (var iw = new IndexWriter(dir, new Analyzer(), overwrite:false))
            {
                iw.Add(2, "title", "The End");
            }
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
        }

        [Test]
        public void Can_append_to_two_fields()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_append_two_fields";
            using (var iw = new IndexWriter(dir, new Analyzer()))
            {
                iw.Add(0, "title", "Hello World!");
                iw.Add(0, "body", "Once upon a time there was a man and a woman.");
            }
            Assert.AreEqual(2, Directory.GetFiles(dir, "*.fld").Length);
            using (var iw = new IndexWriter(dir, new Analyzer(), overwrite:false))
            {
                iw.Add(1, "title", "Goodbye Cruel World.");
                iw.Add(1, "body", "Once upon a time there was a cat and a dog.");
            }
            Assert.AreEqual(4, Directory.GetFiles(dir, "*.fld").Length);
        }
    }
}