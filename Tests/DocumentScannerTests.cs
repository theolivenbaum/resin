using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class DocumentScannerTests
    {
        [Test]
        public void Can_find()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_read";
            const string text = "we all live in a yellow submarine";
            var segments = text.Split(' ');
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    writer.Write(i, "title", string.Join(" ", segments.Take(segments.Length - i)));
                }
            }
            var scanner = new DocumentScanner(dir);
            Assert.AreEqual(7, scanner.GetDocIds("title", "we").Count);
            Assert.AreEqual(6, scanner.GetDocIds("title", "all").Count);
            Assert.AreEqual(5, scanner.GetDocIds("title", "live").Count);
            Assert.AreEqual(4, scanner.GetDocIds("title", "in").Count);
            Assert.AreEqual(3, scanner.GetDocIds("title", "a").Count);
            Assert.AreEqual(2, scanner.GetDocIds("title", "yellow").Count);
            Assert.AreEqual(1, scanner.GetDocIds("title", "submarine").Count);
        }

    }
}