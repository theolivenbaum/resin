using System.IO;
using System.Linq;
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

            tree.AddTest("bad");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(1));
                Assert.IsTrue(near.Contains("bad"));
            }

            tree.AddTest("baby");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(1));
                Assert.IsTrue(near.Contains("bad"));
            }

            tree.AddTest("b");
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

            tree.AddTest("bananas");
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

            tree.AddTest("bank");
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

            tree.AddTest("rambo");
            tree.AddTest("rambo");

            tree.AddTest("2");

            tree.AddTest("rocky");

            tree.AddTest("2");

            tree.AddTest("raiders");

            tree.AddTest("of");
            tree.AddTest("the");
            tree.AddTest("lost");
            tree.AddTest("ark");

            tree.AddTest("rain");

            tree.AddTest("man");

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
            tree.AddTest("xor");
            tree.AddTest("xxx");
            tree.AddTest("donkey");
            tree.AddTest("xavier");
            tree.SerializeMapped(fileName);

            Word word;
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("xxx", out word));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.False(reader.HasWord("baby", out word));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.False(reader.HasWord("dad", out word));
            }

            tree.AddTest("baby");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("xxx", out word));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("baby", out word));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.False(reader.HasWord("dad", out word));
            }

            tree.AddTest("dad");
            tree.AddTest("daddy");
            tree.SerializeMapped(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("xxx", out word));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("baby", out word));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("dad", out word));
            }
            using (var reader = new MappedTrieReader(fileName))
            {
                Assert.True(reader.HasWord("daddy", out word));
            }
        }
    }
}