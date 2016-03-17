using System.IO;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class FieldFileTests
    {
        [Test]
        public void Can_read_write()
        {
            const string fileName = "c:\\temp\\resin_tests\\FieldFileTests\\Can_read_write\\0.fld";
            if (File.Exists(fileName)) File.Delete(fileName);
            using (var writer = new FieldFile(fileName))
            {
                writer.Write(0, "Hello", 0);
                writer.Write(0, "World!", 1);
            }

            Assert.IsTrue(File.Exists(fileName));

            var reader = FieldReader.Load(fileName);
            var terms = reader.GetAllTokens();

            Assert.IsTrue(terms.Contains("Hello"));
            Assert.IsTrue(terms.Contains("World!"));
        }

        [Test]
        public void Can_append()
        {
            const string fileName = "c:\\temp\\resin_tests\\FieldFileTests\\Can_append\\0.fld";
            if (File.Exists(fileName)) File.Delete(fileName);

            using (var writer = new FieldFile(fileName))
            {
                writer.Write(0, "Hello", 0);
            }

            var terms = FieldReader.Load(fileName).GetAllTokens();

            Assert.AreEqual(1, terms.Count);
            Assert.IsTrue(terms.Contains("Hello"));
            Assert.IsFalse(terms.Contains("World!"));

            using (var writer = new FieldFile(fileName))
            {
                writer.Write(0, "World!", 1);
            }

            terms = FieldReader.Load(fileName).GetAllTokens();

            Assert.AreEqual(2, terms.Count);
            Assert.IsTrue(terms.Contains("Hello"));
            Assert.IsTrue(terms.Contains("World!"));
        }
    }
}
