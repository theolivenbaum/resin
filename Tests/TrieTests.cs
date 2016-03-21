using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class TrieTests
    {
        [Test]
        public void Append()
        {
            var trie = new Trie(new[] {"tree"});

            Assert.IsFalse(trie.GetTokens("tree").Contains("trees"));

            Assert.AreEqual(1, trie.GetTokens("tree").Count());
            Assert.AreEqual(0, trie.GetTokens("trees").Count());

            trie.AppendToDescendants("trees");

            Assert.IsTrue(trie.GetTokens("tree").Contains("trees"));

            Assert.AreEqual(2, trie.GetTokens("tree").Count());
            Assert.AreEqual(1, trie.GetTokens("trees").Count());
        }

        [Test]
        public void GetTokens()
        {
            var trie = new Trie(new[] { "tree", "treat", "treaty", "treating", "pre", "prefix" });

            Assert.AreEqual(4, trie.GetTokens("tre").Count());
            Assert.AreEqual(1, trie.GetTokens("tree").Count());
            Assert.AreEqual(3, trie.GetTokens("trea").Count());
            Assert.AreEqual(3, trie.GetTokens("treat").Count());
            Assert.AreEqual(1, trie.GetTokens("treaty").Count());
            Assert.AreEqual(1, trie.GetTokens("treati").Count());
            Assert.AreEqual(1, trie.GetTokens("treatin").Count());
            Assert.AreEqual(1, trie.GetTokens("treating").Count());
            Assert.AreEqual(0, trie.GetTokens("treatings").Count());

            Assert.AreEqual(2, trie.GetTokens("pre").Count());
            Assert.AreEqual(1, trie.GetTokens("pref").Count());

            Assert.IsTrue(trie.GetTokens("tre").Contains("tree"));
            Assert.IsTrue(trie.GetTokens("tre").Contains("treat"));
            Assert.IsTrue(trie.GetTokens("tre").Contains("treaty"));
            Assert.IsTrue(trie.GetTokens("tre").Contains("treating"));
        }

        [Test]
        public void Serialize()
        {
            var trie = new Trie(new[] { "tree", "treaty", "treating", "pre", "prefix" });

            Assert.AreEqual(3, trie.GetTokens("tre").Count());
            Assert.AreEqual(1, trie.GetTokens("tree").Count());
            Assert.AreEqual(2, trie.GetTokens("pre").Count());
            Assert.AreEqual(1, trie.GetTokens("pref").Count());

            trie.Save("serialize.tri");
            trie = Trie.Load("serialize.tri");

            Assert.AreEqual(3, trie.GetTokens("tre").Count());
            Assert.AreEqual(1, trie.GetTokens("tree").Count());
            Assert.AreEqual(2, trie.GetTokens("pre").Count());
            Assert.AreEqual(1, trie.GetTokens("pref").Count());
        }
    }
}