using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class FieldReaderTests
    {
        [Test]
        public void Can_read_field_file()
        {
            const string fileName = "c:\\temp\\resin_tests\\Can_read_field_file\\0.fld";
            if (File.Exists(fileName)) File.Delete(fileName);
            using (var fw = new FieldWriter(fileName))
            {
                fw.Add(0, "hello", 0);
                fw.Add(5, "world", 1);
            }
            var reader = FieldReader.Load(fileName);
            var helloPositionsForDocId0 = reader.GetDocPosition("hello")[0];
            var worldPositionsForDocId5 = reader.GetDocPosition("world")[5];
            Assert.AreEqual(0, helloPositionsForDocId0.First());
            Assert.AreEqual(1, worldPositionsForDocId5.First());
        }

        [Test]
        public void Can_merge_two_positive_field_files()
        {
            const string field0 = "c:\\temp\\resin_tests\\Can_merge_two_positive_field_files\\0.fld";
            const string field1 = "c:\\temp\\resin_tests\\Can_merge_two_positive_field_files\\1.fld";
            if (File.Exists(field0)) File.Delete(field0);
            if (File.Exists(field1)) File.Delete(field1);
            using (var fw = new FieldWriter(field0))
            {
                fw.Add(0, "hello", 0);
            }
            using (var fw = new FieldWriter(field1))
            {
                fw.Add(5, "world", 1);
            }
        }
    }
}
