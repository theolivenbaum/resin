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
            var words = new Trie(new[] { "tree", "trees", "paprika" });

            Assert.True(words.Prefixed("paprika").Contains("paprika"));
            Assert.True(words.Prefixed("tree").Contains("trees"));

            words.Remove("paprika");

            Assert.False(words.Prefixed("paprika").Contains("paprika"));
            Assert.True(words.Prefixed("tree").Contains("tree"));
            Assert.True(words.Prefixed("tree").Contains("trees"));

            words.Remove("tree");

            Assert.False(words.Prefixed("tree").Contains("tree"));
            Assert.True(words.Prefixed("tree").Contains("trees"));
        }

        [Test]
        public void Add()
        {
            var words = new Trie(new[] {"tree"});

            Assert.IsFalse(words.Prefixed("tree").Contains("trees"));

            Assert.AreEqual(1, words.Prefixed("tree").Count());
            Assert.AreEqual(0, words.Prefixed("trees").Count());

            words.Add("trees");

            Assert.IsTrue(words.Prefixed("tree").Contains("trees"));

            Assert.AreEqual(2, words.Prefixed("tree").Count());
            Assert.AreEqual(1, words.Prefixed("trees").Count());
        }

        [Test]
        public void SimilarTo()
        {
            var words = new Trie(new[] { "tree", "treat", "treaty", "treating", "pre" });

            Assert.True(words.Similar("tre", 0).Count() == 0);

            Assert.IsTrue(words.Similar("tre", 1).Contains("tree"));
            Assert.IsFalse(words.Similar("tre", 1).Contains("treat"));
            Assert.IsFalse(words.Similar("tre", 1).Contains("treaty"));
            Assert.IsFalse(words.Similar("tre", 1).Contains("treating"));
            Assert.IsTrue(words.Similar("tre", 1).Contains("pre"));

            Assert.IsTrue(words.Similar("tre", 2).Contains("tree"));
            Assert.IsTrue(words.Similar("tre", 2).Contains("treat"));
            Assert.IsFalse(words.Similar("tre", 2).Contains("treaty"));
            Assert.IsFalse(words.Similar("tre", 2).Contains("treating"));
            Assert.IsTrue(words.Similar("tre", 2).Contains("pre"));

            Assert.IsTrue(words.Similar("tre", 3).Contains("tree"));
            Assert.IsTrue(words.Similar("tre", 3).Contains("treat"));
            Assert.IsTrue(words.Similar("tre", 3).Contains("treaty"));
            Assert.IsFalse(words.Similar("tre", 3).Contains("treating"));
            Assert.IsTrue(words.Similar("tre", 3).Contains("pre"));

            Assert.IsTrue(words.Similar("tre", 4).Contains("tree"));
            Assert.IsTrue(words.Similar("tre", 4).Contains("treat"));
            Assert.IsTrue(words.Similar("tre", 4).Contains("treaty"));
            Assert.IsFalse(words.Similar("tre", 4).Contains("treating"));
            Assert.IsTrue(words.Similar("tre", 4).Contains("pre"));

            Assert.IsTrue(words.Similar("tre", 5).Contains("tree"));
            Assert.IsTrue(words.Similar("tre", 5).Contains("treat"));
            Assert.IsTrue(words.Similar("tre", 5).Contains("treaty"));
            Assert.IsTrue(words.Similar("tre", 5).Contains("treating"));
            Assert.IsTrue(words.Similar("tre", 5).Contains("pre"));
        }

        [Test]
        public void WithPrefix()
        {
            var words = new Trie(new[] { "tree", "treat", "treaty", "treating", "pre", "prefix" });

            Assert.AreEqual(4, words.Prefixed("tre").Count());
            Assert.AreEqual(1, words.Prefixed("tree").Count());
            Assert.AreEqual(3, words.Prefixed("trea").Count());
            Assert.AreEqual(3, words.Prefixed("treat").Count());
            Assert.AreEqual(1, words.Prefixed("treaty").Count());
            Assert.AreEqual(1, words.Prefixed("treati").Count());
            Assert.AreEqual(1, words.Prefixed("treatin").Count());
            Assert.AreEqual(1, words.Prefixed("treating").Count());
            Assert.AreEqual(0, words.Prefixed("treatings").Count());

            Assert.AreEqual(2, words.Prefixed("pre").Count());
            Assert.AreEqual(1, words.Prefixed("pref").Count());

            Assert.IsTrue(words.Prefixed("tre").Contains("tree"));
            Assert.IsTrue(words.Prefixed("tre").Contains("treat"));
            Assert.IsTrue(words.Prefixed("tre").Contains("treaty"));
            Assert.IsTrue(words.Prefixed("tre").Contains("treating"));
        }

        [Test]
        public void Serialize()
        {
            var words = new Trie(new[] { "tree", "treaty", "treating", "pre", "prefix" });

            Assert.AreEqual(3, words.Prefixed("tre").Count());
            Assert.AreEqual(1, words.Prefixed("tree").Count());
            Assert.AreEqual(2, words.Prefixed("pre").Count());
            Assert.AreEqual(1, words.Prefixed("pref").Count());

            words.Save("serialize.tri");
            words = Trie.Load("serialize.tri");

            Assert.AreEqual(3, words.Prefixed("tre").Count());
            Assert.AreEqual(1, words.Prefixed("tree").Count());
            Assert.AreEqual(2, words.Prefixed("pre").Count());
            Assert.AreEqual(1, words.Prefixed("pref").Count());
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