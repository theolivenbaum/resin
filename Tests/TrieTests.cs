using System;
using System.Globalization;
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
            var words = new Trie(new[] { "tree", "treat", "treaty", "treating", "pre" });

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
            var fileName = Setup.Dir + "\\write.trie";
            var dir = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var words = new Trie();
            words.Add("treaty");
            words.Add("treating");
            words.Add("tree");
            words.Add("pre");
            words.Add("prefix");
            words.Add("prefixes");

            Assert.IsTrue(words.HasWord("treaty"));
            Assert.IsTrue(words.HasWord("treating"));
            Assert.IsTrue(words.HasWord("tree"));
            Assert.IsTrue(words.HasWord("pre"));
            Assert.IsTrue(words.HasWord("prefix"));
            Assert.IsTrue(words.HasWord("prefixes"));
            Assert.IsFalse(words.HasWord("prefixx"));

            Assert.AreEqual(3, words.Prefixed("tre").Count());
            Assert.AreEqual(1, words.Prefixed("tree").Count());
            Assert.AreEqual(3, words.Prefixed("pre").Count());
            Assert.AreEqual(2, words.Prefixed("pref").Count());

            Assert.IsTrue(words.Similar("tre", 1).Contains("tree"));
            Assert.IsFalse(words.Similar("tre", 1).Contains("treat"));
            Assert.IsFalse(words.Similar("tre", 1).Contains("treaty"));
            Assert.IsFalse(words.Similar("tre", 1).Contains("treating"));
            Assert.IsTrue(words.Similar("tre", 1).Contains("pre"));

            var fileStream = File.Exists(fileName) ?
                    File.Open(fileName, FileMode.Truncate, FileAccess.Write, FileShare.Read) :
                    File.Open(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read);

            using (var writer = new StreamWriter(fileStream, Encoding.Unicode))
            {
                words.Write(writer, CultureInfo.CurrentCulture);
            }

            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs, Encoding.Unicode))
            using (var reader = new TrieReader(sr))
            {
                Assert.IsTrue(reader.HasWord("treaty"));
                Assert.IsTrue(reader.HasWord("treating"));
                Assert.IsTrue(reader.HasWord("tree"));
                Assert.IsTrue(reader.HasWord("pre"));
                Assert.IsTrue(reader.HasWord("prefix"));
                Assert.IsFalse(reader.HasWord("prefixx"));

                Assert.AreEqual(3, reader.Prefixed("tre").Count());
                Assert.AreEqual(1, reader.Prefixed("tree").Count());
                Assert.AreEqual(3, reader.Prefixed("pre").Count());
                Assert.AreEqual(2, reader.Prefixed("pref").Count());
                Assert.AreEqual(0, reader.Prefixed("cracker").Count());





                //Assert.IsTrue(lz.Similar("tre", 1).Contains("tree"));
                //Assert.IsFalse(lz.Similar("tre", 1).Contains("treat"));
                //Assert.IsFalse(lz.Similar("tre", 1).Contains("treaty"));
                //Assert.IsFalse(lz.Similar("tre", 1).Contains("treating"));
                //Assert.IsTrue(lz.Similar("tre", 1).Contains("pre"));

            }
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs, Encoding.Unicode))
            using (var reader = new TrieReader(sr))
            {
                Assert.IsTrue(reader.HasWord("treating"));
            }

            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs, Encoding.Unicode))
            using (var reader = new TrieReader(sr))
            {
                Assert.IsTrue(reader.HasWord("tree"));


            }
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs, Encoding.Unicode))
            using (var reader = new TrieReader(sr))
            {
                Assert.IsTrue(reader.HasWord("pre"));


            } 
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs, Encoding.Unicode))
            using (var reader = new TrieReader(sr))
            {
                Assert.IsTrue(reader.HasWord("prefix"));


            } 
            using (var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sr = new StreamReader(fs, Encoding.Unicode))
            using (var reader = new TrieReader(sr))
            {
                Assert.IsFalse(reader.HasWord("prefixx"));


            } 
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