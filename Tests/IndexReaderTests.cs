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
        public void Can_read_index()
        {
            const string dir = "c:\\temp\\resin_tests\\Can_read_index";
            using (var w = new IndexWriter(dir, new Analyzer()))
            {
                w.Write(new Document
                {
                    Id = 0,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new[]{"Hello World!"}.ToList()}
                    }
                });
                w.Write(new Document
                {
                    Id = 1,
                    Fields = new Dictionary<string, List<string>>
                    {
                        {"title", new[]{"Goodbye Cruel World."}.ToList()}
                    }
                });
            }
            using(var reader = new IndexReader(new Scanner(dir)))
            {
                var docs = reader.GetDocuments("title", "world").ToList();

                Assert.AreEqual(2, docs.Count);

                docs = reader.GetDocuments("title", "hello").ToList();

                Assert.AreEqual(1, docs.Count);
                Assert.AreEqual("Hello World!", docs[0].Fields["title"][0]);
            }
            

        }

    }
}