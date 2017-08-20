using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Resin;
using DocumentTable;
using StreamIndex;

namespace Tests
{
    [TestClass]
    public class DocumentReaderTests : Setup
    {
        [TestMethod]
        public void Can_read()
        {
            var docs = new List<Document>
            {
                new Document(0, new List<Field>
                {
                    new Field("title", "rambo"),
                    new Field("_id", "0")
                }),
                new Document(1, new List<Field>
                {
                    new Field("title", "rocky"),
                    new Field("_id", "1")
                }),
                new Document(2, new List<Field>
                {
                    new Field("title", "rocky 2"),
                    new Field("_id", "2")
                })
            };

            var fileName = Path.Combine(CreateDir(), "DocumentReaderTests.Can_read");
            var blocks = new Dictionary<int, BlockInfo>();
            var keyIndex = docs[0].ToKeyIndex();
            var revKeyIndex = keyIndex.ToDictionary(x => x.Value, y => y.Key);

            using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            using (var writer = new DocumentWriter(fs, Compression.GZip))
            {
                foreach (var doc in docs)
                {
                    blocks.Add(doc.Id, writer.Write(doc.ToTableRow(keyIndex)));
                }
            }

            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (var reader = new DocumentReader(fs, Compression.GZip, revKeyIndex, 0, leaveOpen:false))
            {
                var doc = reader.Read(new[] { blocks[2] });

                Assert.AreEqual("rocky 2", doc.First().Fields["title"].Value);
            }

            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (var reader = new DocumentReader(fs, Compression.GZip, revKeyIndex, 0, leaveOpen: false))
            {
                var doc = reader.Read(new[] { blocks[1] });

                Assert.AreEqual("rocky", doc.First().Fields["title"].Value);
            }

            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (var reader = new DocumentReader(fs, Compression.GZip, revKeyIndex, 0, leaveOpen: false))
            {
                var doc = reader.Read(new[] { blocks[0] });

                Assert.AreEqual("rambo", doc.First().Fields["title"].Value);
            }

            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (var reader = new DocumentReader(fs, Compression.GZip, revKeyIndex, 0, leaveOpen: false))
            {
                var ds = reader.Read(blocks.Values.OrderBy(b => b.Position).ToList()).ToList();

                Assert.AreEqual(3, docs.Count);

                Assert.IsTrue(ds.Any(d => d.Fields["title"].Value == "rambo"));
                Assert.IsTrue(ds.Any(d => d.Fields["title"].Value == "rocky"));
                Assert.IsTrue(ds.Any(d => d.Fields["title"].Value == "rocky 2"));
            }
        }
    }
}