using System.IO;
using System.Linq;
using NUnit.Framework;
using Resin.IO;
using Resin.IO.Read;
using Resin.IO.Write;

namespace Tests
{
    [TestFixture]
    public class StreamingTrieReaderTests
    {
        [Test]
        public void Can_find_near()
        {
            var fileName = Path.Combine(Setup.Dir, "Can_find_near.tri");

            var tree = new LcrsTrie('\0', false);
            tree.SerializeToTextFile(fileName);

            using (var reader = new TextTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near, Is.Empty);
            }

            tree.Add("bad");
            tree.SerializeToTextFile(fileName);

            using (var reader = new TextTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(1));
                Assert.IsTrue(near.Contains("bad"));
            }

            tree.Add("baby");
            tree.SerializeToTextFile(fileName);

            using (var reader = new TextTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(1));
                Assert.IsTrue(near.Contains("bad"));
            }
            
            tree.Add("b");
            tree.SerializeToTextFile(fileName);

            using (var reader = new TextTrieReader(fileName))
            {
                var near = reader.Near("ba", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(2));
                Assert.IsTrue(near.Contains("bad"));
                Assert.IsTrue(near.Contains("b"));
            }

            using (var reader = new TextTrieReader(fileName))
            {
                var near = reader.Near("ba", 2).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(3));
                Assert.IsTrue(near.Contains("b"));
                Assert.IsTrue(near.Contains("bad"));
                Assert.IsTrue(near.Contains("baby"));
            }

            using (var reader = new TextTrieReader(fileName))
            {
                var near = reader.Near("ba", 0).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(0));
            }

            tree.Add("bananas");
            tree.SerializeToTextFile(fileName);

            using (var reader = new TextTrieReader(fileName))
            {
                var near = reader.Near("ba", 6).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(4));
                Assert.IsTrue(near.Contains("b"));
                Assert.IsTrue(near.Contains("bad"));
                Assert.IsTrue(near.Contains("baby"));
                Assert.IsTrue(near.Contains("bananas"));
            }

            using (var reader = new TextTrieReader(fileName))
            {
                var near = reader.Near("bazy", 1).Select(w => w.Value).ToList();

                Assert.That(near.Count, Is.EqualTo(1));
                Assert.IsTrue(near.Contains("baby"));
            }
            
            tree.Add("bank");
            tree.SerializeToTextFile(fileName);

            using (var reader = new TextTrieReader(fileName))
            {
                var near = reader.Near("bazy", 3).Select(w => w.Value).ToList();

                Assert.AreEqual(4, near.Count);
                Assert.IsTrue(near.Contains("baby"));
                Assert.IsTrue(near.Contains("bank"));
                Assert.IsTrue(near.Contains("bad"));
                Assert.IsTrue(near.Contains("b"));
            }

            using (var reader = new TextTrieReader(fileName))
            {
                var near = reader.Near("baby", 0).Select(w => w.Value).ToList();

                Assert.AreEqual(1, near.Count);
                Assert.IsTrue(near.Contains("baby"));
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

            tree.SerializeToTextFile(fileName);

            var prefixed = new TextTrieReader(fileName).StartsWith("ra").Select(w=>w.Value).ToList();

            Assert.That(prefixed.Count, Is.EqualTo(3));
            Assert.IsTrue(prefixed.Contains("rambo"));
            Assert.IsTrue(prefixed.Contains("raiders"));
            Assert.IsTrue(prefixed.Contains("rain"));
        }

        [Test]
        public void Can_find_exact()
        {
            var fileName = Path.Combine(Setup.Dir, "Can_find_exact.tri");

            var tree = new LcrsTrie('\0', false);
            tree.SerializeToTextFile(fileName);

            using (var reader = new TextTrieReader(fileName))
            {
                Assert.AreEqual(1, tree.GetWeight());

                Assert.False(reader.HasWord("xxx"));
            }

            tree.Add("xxx");
            tree.SerializeToTextFile(fileName);

            using (var reader = new TextTrieReader(fileName))
            {
                Assert.AreEqual(4, tree.GetWeight());

                Assert.True(reader.HasWord("xxx"));
            }
            using (var reader = new TextTrieReader(fileName))
            {
                Assert.False(reader.HasWord("baby"));
            }
            using (var reader = new TextTrieReader(fileName))
            {
                Assert.False(reader.HasWord("dad"));
            }

            tree.Add("baby");
            tree.SerializeToTextFile(fileName);

            using (var reader = new TextTrieReader(fileName))
            {
                Assert.AreEqual(8, tree.GetWeight());

                Assert.True(reader.HasWord("xxx"));
            }
            using (var reader = new TextTrieReader(fileName))
            {
                Assert.True(reader.HasWord("baby"));
            }
            using (var reader = new TextTrieReader(fileName))
            {
                Assert.False(reader.HasWord("dad"));
            }

            tree.Add("dad");
            tree.SerializeToTextFile(fileName);

            using (var reader = new TextTrieReader(fileName))
            {
                Assert.True(reader.HasWord("xxx"));
            }
            using (var reader = new TextTrieReader(fileName))
            {
                Assert.True(reader.HasWord("baby"));
            }
            using (var reader = new TextTrieReader(fileName))
            {
                Assert.True(reader.HasWord("dad"));
            }
        }
    }
}