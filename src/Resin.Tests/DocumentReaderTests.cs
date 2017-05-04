//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using NUnit.Framework;
//using Resin;
//using Resin.IO;
//using Resin.IO.Read;
//using Resin.IO.Write;

//namespace Tests
//{
//    [TestFixture]
//    public class DocumentReaderTests
//    {
//        [Test]
//        public void Can_read()
//        {
//            var docs = new List<Document>
//            {
//                new Document(new Dictionary<string, string>
//                {
//                    {"title", "rambo"}, 
//                    {"_id", "0"}
//                }),
//                new Document(new Dictionary<string, string>
//                {
//                    {"title", "rocky"}, 
//                    {"_id", "1"}
//                }),
//                new Document(new Dictionary<string, string>
//                {
//                    {"title", "rocky 2"}, 
//                    {"_id", "2"}
//                })
//            };

//            var fileName = Path.Combine(Setup.Dir, "DocumentReaderTests.Can_read");
//            var blocks = new Dictionary<int, BlockInfo>();

//            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
//            using (var writer = new DocumentWriter(fs, false))
//            {
//                var index = 0;
//                foreach (var doc in docs)
//                {
//                    doc.Id = index++;
//                    blocks.Add(doc.Id, writer.Write(doc));
//                }
//            }

//            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
//            using (var reader = new DocumentReader(fs, false))
//            {
//                var doc = reader.Get(new[] {blocks[2]});

//                Assert.That(doc.First().Id, Is.EqualTo(2));
//            }

//            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
//            using (var reader = new DocumentReader(fs, false))
//            {
//                var doc = reader.Get(new[] { blocks[1] });

//                Assert.That(doc.First().Id, Is.EqualTo(1));
//            }

//            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
//            using (var reader = new DocumentReader(fs, false))
//            {
//                var doc = reader.Get(new[] { blocks[0] });

//                Assert.That(doc.First().Id, Is.EqualTo(0));
//            }

//            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
//            using (var reader = new DocumentReader(fs, false))
//            {
//                var ds = reader.Get(blocks.Values.OrderBy(b=>b.Position).ToList()).ToList();

//                Assert.That(docs.Count, Is.EqualTo(3));

//                Assert.IsTrue(ds.Any(d => d.Id == 0));
//                Assert.IsTrue(ds.Any(d => d.Id == 1));
//                Assert.IsTrue(ds.Any(d => d.Id == 2));
//            }
//        }
//    }
//}