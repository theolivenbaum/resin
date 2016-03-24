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

            Assert.True(trie.StartingWith("paprika").Contains("paprika"));
            Assert.True(trie.StartingWith("tree").Contains("trees"));

            trie.Remove("paprika");

            Assert.False(trie.StartingWith("paprika").Contains("paprika"));
            Assert.True(trie.StartingWith("tree").Contains("tree"));
            Assert.True(trie.StartingWith("tree").Contains("trees"));

            trie.Remove("tree");

            Assert.False(trie.StartingWith("tree").Contains("tree"));
            Assert.True(trie.StartingWith("tree").Contains("trees"));
        }

        [Test]
        public void Append()
        {
            var trie = new Trie(new[] {"tree"});

            Assert.IsFalse(trie.StartingWith("tree").Contains("trees"));

            Assert.AreEqual(1, trie.StartingWith("tree").Count());
            Assert.AreEqual(0, trie.StartingWith("trees").Count());

            trie.Add("trees");

            Assert.IsTrue(trie.StartingWith("tree").Contains("trees"));

            Assert.AreEqual(2, trie.StartingWith("tree").Count());
            Assert.AreEqual(1, trie.StartingWith("trees").Count());
        }

        [Test]
        public void GetTokens()
        {
            var trie = new Trie(new[] { "tree", "treat", "treaty", "treating", "pre", "prefix" });

            Assert.AreEqual(4, trie.StartingWith("tre").Count());
            Assert.AreEqual(1, trie.StartingWith("tree").Count());
            Assert.AreEqual(3, trie.StartingWith("trea").Count());
            Assert.AreEqual(3, trie.StartingWith("treat").Count());
            Assert.AreEqual(1, trie.StartingWith("treaty").Count());
            Assert.AreEqual(1, trie.StartingWith("treati").Count());
            Assert.AreEqual(1, trie.StartingWith("treatin").Count());
            Assert.AreEqual(1, trie.StartingWith("treating").Count());
            Assert.AreEqual(0, trie.StartingWith("treatings").Count());

            Assert.AreEqual(2, trie.StartingWith("pre").Count());
            Assert.AreEqual(1, trie.StartingWith("pref").Count());

            Assert.IsTrue(trie.StartingWith("tre").Contains("tree"));
            Assert.IsTrue(trie.StartingWith("tre").Contains("treat"));
            Assert.IsTrue(trie.StartingWith("tre").Contains("treaty"));
            Assert.IsTrue(trie.StartingWith("tre").Contains("treating"));
        }

        [Test]
        public void Serialize()
        {
            var trie = new Trie(new[] { "tree", "treaty", "treating", "pre", "prefix" });

            Assert.AreEqual(3, trie.StartingWith("tre").Count());
            Assert.AreEqual(1, trie.StartingWith("tree").Count());
            Assert.AreEqual(2, trie.StartingWith("pre").Count());
            Assert.AreEqual(1, trie.StartingWith("pref").Count());

            trie.Save("serialize.tri");
            trie = Trie.Load("serialize.tri");

            Assert.AreEqual(3, trie.StartingWith("tre").Count());
            Assert.AreEqual(1, trie.StartingWith("tree").Count());
            Assert.AreEqual(2, trie.StartingWith("pre").Count());
            Assert.AreEqual(1, trie.StartingWith("pref").Count());
        }
    }

    [TestFixture]
    public class LevenshteinTests
    {
        [Test]
        public void Can_calculate_distance()
        {
            Assert.AreEqual(0, Levenshtein.Distance("rambo", "rambo"));

            Assert.AreEqual(1, Levenshtein.Distance("rambo", "ramb")); // 1 del
            Assert.AreEqual(2, Levenshtein.Distance("rambo", "amb")); // 2 del
            Assert.AreEqual(3, Levenshtein.Distance("rambo", "am")); // 3 del

            Assert.AreEqual(1, Levenshtein.Distance("rambo", "rxmbo")); // 1 sub
            Assert.AreEqual(2, Levenshtein.Distance("rambo", "xxmbo")); // 2 sub
            Assert.AreEqual(3, Levenshtein.Distance("rambo", "xxmbx")); // 3 sub

            Assert.AreEqual(1, Levenshtein.Distance("rambo", "rambos")); // 1 ins
            Assert.AreEqual(2, Levenshtein.Distance("rambo", "arambos")); // 2 ins
            Assert.AreEqual(3, Levenshtein.Distance("rambo", "arammbos")); // 3 ins
        }
    }
}