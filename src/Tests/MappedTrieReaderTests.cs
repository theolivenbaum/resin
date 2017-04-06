using System.IO;
using System.Linq;
using NUnit.Framework;
using Resin.IO;
using Resin.IO.Read;

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
            tree.Serialize(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near, Is.Empty);
            }

            tree.Add("bad");
            tree.Serialize(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(1));
                Assert.IsTrue(near.Contains("bad"));
            }

            tree.Add("baby");
            tree.Serialize(fileName);

            using (var reader = new MappedTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(1));
                Assert.IsTrue(near.Contains("bad"));
            }

            tree.Add("b");
            tree.Serialize(fileName);

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
            tree.Serialize(fileName);

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
            tree.Serialize(fileName);

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

            tree.Serialize(fileName);

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
            tree.Serialize(fileName);

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

            tree.Add("baby");
            tree.Serialize(fileName);

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

            tree.Add("dad");
            tree.Add("daddy");
            tree.Serialize(fileName);

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