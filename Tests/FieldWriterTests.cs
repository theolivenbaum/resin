using System.IO;
using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class FieldWriterTests
    {
        [Test]
        public void Can_read_write()
        {
            const string fileName = "c:\\temp\\resin_tests\\FieldWriterTests\\Can_read_write\\0.fld";
            if (File.Exists(fileName)) File.Delete(fileName);
            using (var writer = new FieldWriter(fileName))
            {
                writer.Write(0, "Hello", 0);
                writer.Write(0, "World!", 1);
            }

            Assert.IsTrue(File.Exists(fileName));

            var reader = FieldReader.Load(fileName);
            var terms = reader.GetAllTokens().Select(t => t.Token).ToList();

            Assert.IsTrue(terms.Contains("Hello"));
            Assert.IsTrue(terms.Contains("World!"));
        }

        [Test]
        public void Can_append()
        {
            const string fileName = "c:\\temp\\resin_tests\\FieldWriterTests\\Can_append\\0.fld";
            if (File.Exists(fileName)) File.Delete(fileName);

            using (var writer = new FieldWriter(fileName))
            {
                writer.Write(0, "Hello", 0);
            }

            var terms = FieldReader.Load(fileName).GetAllTokens().Select(t=>t.Token).ToList();

            Assert.AreEqual(1, terms.Count);
            Assert.IsTrue(terms.Contains("Hello"));
            Assert.IsFalse(terms.Contains("World!"));

            using (var writer = new FieldWriter(fileName))
            {
                writer.Write(0, "World!", 1);
            }

            terms = FieldReader.Load(fileName).GetAllTokens().Select(t => t.Token).ToList();

            Assert.AreEqual(2, terms.Count);
            Assert.IsTrue(terms.Contains("Hello"));
            Assert.IsTrue(terms.Contains("World!"));
        }
    }
}
