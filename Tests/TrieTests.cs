using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Resin;
using Resin.IO;

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
            var words = new Trie(new[] { "tree", "trees" });

            Assert.IsTrue(words.HasWord("tree"));
            Assert.IsTrue(words.HasWord("trees"));
            Assert.IsFalse(words.HasWord("tre"));
            Assert.IsFalse(words.HasWord("treesesses"));

            words.Add("tre");

            Assert.IsTrue(words.HasWord("tree"));
            Assert.IsTrue(words.HasWord("trees"));
            Assert.IsTrue(words.HasWord("tre"));
            Assert.IsFalse(words.HasWord("treesesses"));

            words.Add("treesesses");

            Assert.IsTrue(words.HasWord("tree"));
            Assert.IsTrue(words.HasWord("trees"));
            Assert.IsTrue(words.HasWord("tre"));
            Assert.IsTrue(words.HasWord("treesesses"));
        }

        [Test]
        public void Exact()
        {
            var words = new Trie(new[] { "ring", "ringo", "apple" });

            Assert.IsFalse(words.HasWord("rin"));
            Assert.IsTrue(words.HasWord("ring"));
            Assert.IsFalse(words.HasWord("ringa"));
            Assert.IsTrue(words.HasWord("ringo"));
            Assert.IsFalse(words.HasWord("appl"));
            Assert.IsTrue(words.HasWord("apple"));
            Assert.IsFalse(words.HasWord("apples"));
        }

        [Test]
        public void SimilarTo()
        {
            var words = new Trie(new[] { "tree", "treat", "treaty", "treating", "pre", "ring", "rings" });

            Assert.AreEqual(0, words.Similar("tre", 0).Count());
            Assert.AreEqual(1, words.Similar("tree", 0).Count());

            Assert.IsTrue(words.Similar("tree", 0).Contains("tree"));
            Assert.IsTrue(words.Similar("tree", 1).Contains("tree"));
            Assert.IsTrue(words.Similar("tree", 2).Contains("tree"));

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

            Assert.AreEqual(4, words.Similar("tree", 3).Count());
            Assert.IsTrue(words.Similar("tree", 3).Contains("tree"));
            Assert.IsTrue(words.Similar("tree", 3).Contains("treat"));
            Assert.IsTrue(words.Similar("tree", 3).Contains("treaty"));
            Assert.IsTrue(words.Similar("tree", 3).Contains("pre"));

            Assert.AreEqual(2, words.Similar("ring", 1).Count());
            Assert.IsTrue(words.Similar("ring", 1).Contains("ring"));
            Assert.IsTrue(words.Similar("ring", 1).Contains("rings"));

            Assert.AreEqual(1, words.Similar("ring", 0).Count());
            Assert.IsTrue(words.Similar("ring", 0).Contains("ring"));
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
        public void Write_and_read()
        {
            var fileId = "write";

            var words = new Trie();
            words.Add("treaty");
            words.Add("treating");
            words.Add("tree");
            words.Add("pre");
            words.Add("prefix");
            words.Add("prefixes");
            words.Add("ring");
            words.Add("rings");

            Assert.IsTrue(words.HasWord("treaty"));
            Assert.IsTrue(words.HasWord("treating"));
            Assert.IsTrue(words.HasWord("tree"));
            Assert.IsTrue(words.HasWord("pre"));
            Assert.IsTrue(words.HasWord("prefix"));
            Assert.IsTrue(words.HasWord("prefixes"));
            Assert.IsTrue(words.HasWord("ring"));
            Assert.IsTrue(words.HasWord("rings"));
            Assert.IsFalse(words.HasWord("prefixx"));

            Assert.AreEqual(3, words.Prefixed("tre").Count());
            Assert.AreEqual(1, words.Prefixed("tree").Count());
            Assert.AreEqual(3, words.Prefixed("pre").Count());
            Assert.AreEqual(2, words.Prefixed("pref").Count());
            Assert.AreEqual(2, words.Prefixed("ring").Count());
            Assert.AreEqual(0, words.Prefixed("cracker").Count());

            Assert.IsTrue(words.Similar("tre", 1).Contains("tree"));
            Assert.IsTrue(words.Similar("tre", 1).Contains("pre"));
            Assert.IsFalse(words.Similar("tre", 1).Contains("treat"));
            Assert.IsFalse(words.Similar("tre", 1).Contains("treaty"));
            Assert.IsFalse(words.Similar("tre", 1).Contains("treating"));

            Assert.AreEqual(3, words.Similar("treaty", 3).Count());
            Assert.IsTrue(words.Similar("treaty", 3).Contains("treaty"));
            Assert.IsTrue(words.Similar("treaty", 3).Contains("treating"));
            Assert.IsTrue(words.Similar("treaty", 3).Contains("tree"));

            Assert.AreEqual(3, words.Similar("tree", 3).Count());
            Assert.IsTrue(words.Similar("tree", 3).Contains("tree"));
            Assert.IsTrue(words.Similar("tree", 3).Contains("treaty"));
            Assert.IsTrue(words.Similar("tree", 3).Contains("pre"));

            Assert.AreEqual(2, words.Similar("ring", 1).Count());
            Assert.IsTrue(words.Similar("ring", 1).Contains("ring"));
            Assert.IsTrue(words.Similar("ring", 1).Contains("rings"));

            Assert.AreEqual(1, words.Similar("ring", 0).Count());
            Assert.IsTrue(words.Similar("ring", 0).Contains("ring"));

            using (var writer = new TrieWriter(fileId, Setup.Dir))
            {
                writer.Write(words);
            }

            Trie serialized;
            using (var fs = File.Open(Path.Combine(Setup.Dir, fileId + ".trie"), FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs, Encoding.Unicode))
            using (var reader = new TrieReader(sr))
            {
                serialized = reader.ReadWholeTree();
            }
            Assert.IsTrue(serialized.HasWord("treaty"));
            Assert.IsTrue(serialized.HasWord("treating"));
            Assert.IsTrue(serialized.HasWord("tree"));
            Assert.IsTrue(serialized.HasWord("pre"));
            Assert.IsTrue(serialized.HasWord("prefix"));
            Assert.IsTrue(serialized.HasWord("ring"));
            Assert.IsTrue(serialized.HasWord("rings"));
            Assert.IsFalse(serialized.HasWord("prefixx"));

            Assert.AreEqual(3, serialized.Prefixed("tre").Count());
            Assert.AreEqual(1, serialized.Prefixed("tree").Count());
            Assert.AreEqual(3, serialized.Prefixed("pre").Count());
            Assert.AreEqual(2, serialized.Prefixed("pref").Count());
            Assert.AreEqual(2, serialized.Prefixed("ring").Count());
            Assert.AreEqual(0, serialized.Prefixed("cracker").Count());

            Assert.IsTrue(serialized.Similar("tre", 1).Contains("tree"));
            Assert.IsTrue(serialized.Similar("tre", 1).Contains("pre"));
            Assert.IsFalse(serialized.Similar("tre", 1).Contains("treat"));
            Assert.IsFalse(serialized.Similar("tre", 1).Contains("treaty"));
            Assert.IsFalse(serialized.Similar("tre", 1).Contains("treating"));

            Assert.AreEqual(3, serialized.Similar("treaty", 3).Count());
            Assert.IsTrue(serialized.Similar("treaty", 3).Contains("treaty"));
            Assert.IsTrue(serialized.Similar("treaty", 3).Contains("treating"));
            Assert.IsTrue(serialized.Similar("treaty", 3).Contains("tree"));

            Assert.AreEqual(3, serialized.Similar("tree", 3).Count());
            Assert.IsTrue(serialized.Similar("tree", 3).Contains("tree"));
            Assert.IsTrue(serialized.Similar("tree", 3).Contains("treaty"));
            Assert.IsTrue(serialized.Similar("tree", 3).Contains("pre"));

            Assert.AreEqual(2, serialized.Similar("ring", 1).Count());
            Assert.IsTrue(serialized.Similar("ring", 1).Contains("ring"));
            Assert.IsTrue(serialized.Similar("ring", 1).Contains("rings"));

            Assert.AreEqual(1, words.Similar("ring", 0).Count());
            Assert.IsTrue(words.Similar("ring", 0).Contains("ring"));
        }

        [Test]
        public void Contains()
        {
            var words = new Trie(new[] { "tree", "treat", "treaty", "treating", "pre" });

            Assert.IsTrue(words.HasWord("tree"));
            Assert.IsTrue(words.HasWord("treat"));
            Assert.IsTrue(words.HasWord("treaty"));
            Assert.IsTrue(words.HasWord("treating"));
            Assert.IsTrue(words.HasWord("pre"));

            Assert.IsFalse(words.HasWord("pee"));
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