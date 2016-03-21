using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class TrieTests
    {
        [Test]
        public void WordsStartingWith()
        {
            var trie = new Trie(new[] { "tree", "treat", "treaty", "treating", "pre", "prefix" });

            Assert.AreEqual(4, trie.WordsStartingWith("tre").Count());
            Assert.AreEqual(1, trie.WordsStartingWith("tree").Count());
            Assert.AreEqual(3, trie.WordsStartingWith("trea").Count());
            Assert.AreEqual(3, trie.WordsStartingWith("treat").Count());
            Assert.AreEqual(1, trie.WordsStartingWith("treaty").Count());
            Assert.AreEqual(1, trie.WordsStartingWith("treati").Count());
            Assert.AreEqual(1, trie.WordsStartingWith("treatin").Count());
            Assert.AreEqual(1, trie.WordsStartingWith("treating").Count());
            Assert.AreEqual(0, trie.WordsStartingWith("treatings").Count());

            Assert.AreEqual(2, trie.WordsStartingWith("pre").Count());
            Assert.AreEqual(1, trie.WordsStartingWith("pref").Count());

            Assert.IsTrue(trie.WordsStartingWith("tre").Contains("tree"));
            Assert.IsTrue(trie.WordsStartingWith("tre").Contains("treat"));
            Assert.IsTrue(trie.WordsStartingWith("tre").Contains("treaty"));
            Assert.IsTrue(trie.WordsStartingWith("tre").Contains("treating"));
        }

        [Test]
        public void Serialize()
        {
            var trie = new Trie(new[] { "tree", "treaty", "treating", "pre", "prefix" });

            Assert.AreEqual(3, trie.WordsStartingWith("tre").Count());
            Assert.AreEqual(1, trie.WordsStartingWith("tree").Count());
            Assert.AreEqual(2, trie.WordsStartingWith("pre").Count());
            Assert.AreEqual(1, trie.WordsStartingWith("pref").Count());

            trie.Save("serialize.tri");
            trie = Trie.Load("serialize.tri");

            Assert.AreEqual(3, trie.WordsStartingWith("tre").Count());
            Assert.AreEqual(1, trie.WordsStartingWith("tree").Count());
            Assert.AreEqual(2, trie.WordsStartingWith("pre").Count());
            Assert.AreEqual(1, trie.WordsStartingWith("pref").Count());
        }
    }
}