using System.IO;
using Lzo64;
using NUnit.Framework;
using Resin.IO;

namespace Tests
{
    [TestFixture]
    public class CompressionTests
    {
        [Test]
        public void Can_compress()
        {
            var fileName = Path.Combine(Setup.Dir, "dasddasdas.cdd");
            var file = new DocFieldFile("doc0", "field0", "Hello!");
            using (var fs = File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var memStream = new MemoryStream())
            {
                FileBase.Serializer.Serialize(memStream, file);
                var bytes = memStream.ToArray();
                var comp = new LZOCompressor();
                var compressed = comp.Compress(bytes);
                fs.Write(compressed, 0, compressed.Length);
            }

            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var memStream = new MemoryStream())
            {
                fs.CopyTo(memStream);
                var bytes = memStream.ToArray();
                var comp = new LZOCompressor();
                var decompressed = comp.Decompress(bytes);
                var obj = (DocFieldFile) FileBase.Serializer.Deserialize(new MemoryStream(decompressed));
                Assert.AreEqual("Hello!", obj.Value);
            }
        }
    }
}