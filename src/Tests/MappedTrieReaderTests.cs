using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Resin.IO;
using Resin.IO.Read;
using Resin.IO.Write;

namespace Tests
{
    [TestFixture]
    public class MappedTrieReaderTests
    {
        [Test]
        public void Can_serialize_struct()
        {
            var node = new LcrsNode("a0010");
            var bytes = LcrsTrieSerializer.TypeToBytes(node);
            var resurrected = LcrsTrieSerializer.BytesToType<LcrsNode>(bytes);

            Assert.That(resurrected.Value, Is.EqualTo(node.Value));
            Assert.IsTrue(resurrected.EndOfWord);
        }

        [Test]
        public void Can_deserialize_struct_from_disk()
        {
            var fileName = Path.Combine(Setup.Dir, "Can_deserialize_struct_from_disk.tri");
            var node = new LcrsNode("ä0010");
            using (var fs = new FileStream(fileName, FileMode.Create))
            {
                var bytes = LcrsTrieSerializer.TypeToBytes(node);
                fs.Write(bytes, 0, bytes.Length);
            }
            using (var fs = new FileStream(fileName, FileMode.Open))
            {
                var len = Marshal.SizeOf(typeof(LcrsNode));
                var buffer = new byte[len];
                fs.Read(buffer, 0, buffer.Length);
                var resurrected = LcrsTrieSerializer.BytesToType<LcrsNode>(buffer);

                Assert.That(resurrected.Value, Is.EqualTo(node.Value));
                Assert.IsTrue(resurrected.EndOfWord);
            }
        }

        [Test]
        public void Can_deserialize_struct_from_disk_with_offset()
        {
            var fileName = Path.Combine(Setup.Dir, "Can_deserialize_struct_from_disk_with_offset.tri");
            var node1 = new LcrsNode("a0010");
            var node2 = new LcrsNode("b0010");
            using (var fs = new FileStream(fileName, FileMode.Create))
            {
                var bytes = LcrsTrieSerializer.TypeToBytes(node1);
                fs.Write(bytes, 0, bytes.Length);

                bytes = LcrsTrieSerializer.TypeToBytes(node2);
                fs.Write(bytes, 0, bytes.Length);
            }
            using (var fs = new FileStream(fileName, FileMode.Open))
            {
                var len = Marshal.SizeOf(typeof(LcrsNode));
                var buffer = new byte[len];
                fs.Seek(len, SeekOrigin.Begin);
                fs.Read(buffer, 0, buffer.Length);
                var resurrected = LcrsTrieSerializer.BytesToType<LcrsNode>(buffer);

                Assert.That(resurrected.Value, Is.EqualTo(node2.Value));
            }
        }

        [Test]
        public void Can_find_near()
        {
            var fileName = Path.Combine(Setup.Dir, "Can_find_near.tri");

            var tree = new LcrsTrie('\0', false);
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near, Is.Empty);
            }

            tree.Add("bad");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(1));
                Assert.IsTrue(near.Contains("bad"));
            }

            tree.Add("baby");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(1));
                Assert.IsTrue(near.Contains("bad"));
            }

            tree.Add("b");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(2));
                Assert.IsTrue(near.Contains("bad"));
                Assert.IsTrue(near.Contains("b"));
            }

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 2).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(3));
                Assert.IsTrue(near.Contains("b"));
                Assert.IsTrue(near.Contains("bad"));
                Assert.IsTrue(near.Contains("baby"));
            }

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 0).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(0));
            }

            tree.Add("bananas");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 6).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(4));
                Assert.IsTrue(near.Contains("b"));
                Assert.IsTrue(near.Contains("bad"));
                Assert.IsTrue(near.Contains("baby"));
                Assert.IsTrue(near.Contains("bananas"));
            }

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("bazy", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(1));
                Assert.IsTrue(near.Contains("baby"));
            }

            tree.Add("bank");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("bazy", 3).Select(w => w.Value).ToList();

                Assert.AreEqual(4, near.Count);
                Assert.IsTrue(near.Contains("baby"));
                Assert.IsTrue(near.Contains("bank"));
                Assert.IsTrue(near.Contains("bad"));
                Assert.IsTrue(near.Contains("b"));
            }
        }

        [Test]
        public void Can_find_prefixed()
        {
            var fileName = Path.Combine(Setup.Dir, "Can_find_prefixed.tri");

            var tree = new LcrsTrie('\0', false);

            tree.Add("rambo");
            tree.Add("rambo");

            tree.Add("2");

            tree.Add("rocky");

            tree.Add("2");

            tree.Add("raiders");

            tree.Add("of");
            tree.Add("the");
            tree.Add("lost");
            tree.Add("ark");

            tree.Add("rain");

            tree.Add("man");

            tree.SerializeMapped(fileName);

            var prefixed = new MappedTrieReader(fileName).StartsWith("ra").Select(w => w.Value).ToList();

            Assert.That(prefixed.Count, Is.EqualTo(3));
            Assert.IsTrue(prefixed.Contains("rambo"));
            Assert.IsTrue(prefixed.Contains("raiders"));
            Assert.IsTrue(prefixed.Contains("rain"));
        }

        [Test]
        public void Can_find_exact()
        {
            var fileName = Path.Combine(Setup.Dir, "Can_find_exact_mm.tri");

            var tree = new LcrsTrie('\0', false);
            tree.Add("xor");
            tree.Add("xxx");
            tree.Add("donkey");
            tree.Add("xavier");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("xxx"));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.False(reader.HasWord("baby"));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.False(reader.HasWord("dad"));
            }

            tree.Add("baby");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("xxx"));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("baby"));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.False(reader.HasWord("dad"));
            }

            tree.Add("dad");
            tree.Add("daddy");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("xxx"));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("baby"));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("dad"));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("daddy"));
            }
        }
    }
}