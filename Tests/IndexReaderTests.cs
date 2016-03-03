using System.IO;
using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class IndexReaderTests
    {
        [Test]
        public void Can_read_index()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_read_index";
            using (var iw = new IndexWriter(dir, new Analyzer()))
            {
                iw.Add(0, "title", "Hello World!");
                iw.Add(1, "title", "Goodbye Cruel World.");
            }
            var reader = new IndexReader(new DocumentScanner(dir));
            var docs = reader.GetDocuments("title", "world").ToList();

            Assert.AreEqual(2, docs.Count);

            docs = reader.GetDocuments("title", "hello").ToList();

            Assert.AreEqual(1, docs.Count);
            Assert.AreEqual("Hello World!", docs[0]["title"][0]);

        }

    }
}