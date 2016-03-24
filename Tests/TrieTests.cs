using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class TrieTests
    {
        [Test]
        public void Remove()
        {
            var trie = new Trie(new[] { "tree", "trees", "paprika" });

            Assert.True(trie.FindWords("paprika").Contains("paprika"));
            Assert.True(trie.FindWords("tree").Contains("trees"));

            trie.Remove("paprika");

            Assert.False(trie.FindWords("paprika").Contains("paprika"));
            Assert.True(trie.FindWords("tree").Contains("tree"));
            Assert.True(trie.FindWords("tree").Contains("trees"));

            trie.Remove("tree");

            Assert.False(trie.FindWords("tree").Contains("tree"));
            Assert.True(trie.FindWords("tree").Contains("trees"));
        }

        [Test]
        public void Append()
        {
            var trie = new Trie(new[] {"tree"});

            Assert.IsFalse(trie.FindWords("tree").Contains("trees"));

            Assert.AreEqual(1, trie.FindWords("tree").Count());
            Assert.AreEqual(0, trie.FindWords("trees").Count());

            trie.AddWord("trees");

            Assert.IsTrue(trie.FindWords("tree").Contains("trees"));

            Assert.AreEqual(2, trie.FindWords("tree").Count());
            Assert.AreEqual(1, trie.FindWords("trees").Count());
        }

        [Test]
        public void GetTokens()
        {
            var trie = new Trie(new[] { "tree", "treat", "treaty", "treating", "pre", "prefix" });

            Assert.AreEqual(4, trie.FindWords("tre").Count());
            Assert.AreEqual(1, trie.FindWords("tree").Count());
            Assert.AreEqual(3, trie.FindWords("trea").Count());
            Assert.AreEqual(3, trie.FindWords("treat").Count());
            Assert.AreEqual(1, trie.FindWords("treaty").Count());
            Assert.AreEqual(1, trie.FindWords("treati").Count());
            Assert.AreEqual(1, trie.FindWords("treatin").Count());
            Assert.AreEqual(1, trie.FindWords("treating").Count());
            Assert.AreEqual(0, trie.FindWords("treatings").Count());

            Assert.AreEqual(2, trie.FindWords("pre").Count());
            Assert.AreEqual(1, trie.FindWords("pref").Count());

            Assert.IsTrue(trie.FindWords("tre").Contains("tree"));
            Assert.IsTrue(trie.FindWords("tre").Contains("treat"));
            Assert.IsTrue(trie.FindWords("tre").Contains("treaty"));
            Assert.IsTrue(trie.FindWords("tre").Contains("treating"));
        }

        [Test]
        public void Serialize()
        {
            var trie = new Trie(new[] { "tree", "treaty", "treating", "pre", "prefix" });

            Assert.AreEqual(3, trie.FindWords("tre").Count());
            Assert.AreEqual(1, trie.FindWords("tree").Count());
            Assert.AreEqual(2, trie.FindWords("pre").Count());
            Assert.AreEqual(1, trie.FindWords("pref").Count());

            trie.Save("serialize.tri");
            trie = Trie.Load("serialize.tri");

            Assert.AreEqual(3, trie.FindWords("tre").Count());
            Assert.AreEqual(1, trie.FindWords("tree").Count());
            Assert.AreEqual(2, trie.FindWords("pre").Count());
            Assert.AreEqual(1, trie.FindWords("pref").Count());
        }
    }
}