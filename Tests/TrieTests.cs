using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class TrieTests
    {
        [Test]
        public void Descendants()
        {
            var trie = new Trie(new[] { "tre", "tree", "trees" });

            Assert.AreEqual(5, trie.Descendants().ToList().Count);
            Assert.AreEqual(4, trie.Descendants().First().Descendants().ToList().Count);
        }

        [Test]
        public void DescendantsWhere()
        {
            var trie = new Trie(new[] { "tre", "tree", "trees" });
            var words = trie.Descendants().Where(t => t.Eow).Select(t => t.Path()).ToList();

            Assert.AreEqual(3, words.Count);
        }

        [Test]
        public void WordsStartingWith()
        {
            var trie = new Trie(new[] { "tree", "trees", "pre", "prefix" });
            var wordsStartingWithTre = trie.WordsStartingWith("tre").ToList();

            Assert.AreEqual(2, wordsStartingWithTre.Count);
        }
    }
}