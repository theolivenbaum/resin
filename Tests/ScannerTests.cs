using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class ScannerTests
    {
        [Test]
        public void Can_scan()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_find";
            const string text = "we all live in a yellow submarine";
            using (var writer = new IndexWriter(dir, new Analyzer()))
            {
                writer.Write(new Document
                {
                    Fields = new Dictionary<string, List<string>>
                        {
                            {"title", text.Split(' ').ToList()}
                        }
                });
            }
            var scanner = new Scanner(dir);
            Assert.AreEqual(1, scanner.GetDocIds("title", "we").Count);
            Assert.AreEqual(1, scanner.GetDocIds("title", "all").Count);
            Assert.AreEqual(1, scanner.GetDocIds("title", "live").Count);
            Assert.AreEqual(1, scanner.GetDocIds("title", "in").Count);
            Assert.AreEqual(1, scanner.GetDocIds("title", "a").Count);
            Assert.AreEqual(1, scanner.GetDocIds("title", "yellow").Count);
            Assert.AreEqual(1, scanner.GetDocIds("title", "submarine").Count);
        }
    }
}