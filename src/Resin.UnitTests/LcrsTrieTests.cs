using System.Linq;
using Resin.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    [TestClass]
    public class LcrsTrieTests
    {
        [TestMethod]
        public void Can_find_within_range()
        {
            var trie = new LcrsTrie();
            trie.Add("ape");
            trie.Add("app");
            trie.Add("apple");
            trie.Add("banana");
            trie.Add("bananas");
            trie.Add("xanax");
            trie.Add("xxx");

            var words = trie.WithinRange("app", "xerox").ToList();

            Assert.AreEqual(4, words.Count);
            Assert.AreEqual("apple", words[1]);
            Assert.AreEqual("banana", words[2]);
            Assert.AreEqual("bananas", words[3]);
            Assert.AreEqual("xanax", words[4]);
        }

        [TestMethod]
        public void Can_append_tries()
        {
            var one = new LcrsTrie('\0', false);
            one.Add("ape");
            one.Add("app");
            one.Add("banana");

            var two = new LcrsTrie('\0', false);
            two.Add("apple");
            two.Add("banana");

            one.Merge(two);

            Word found;
            Assert.IsTrue(one.HasWord("ape", out found));
            Assert.IsTrue(one.HasWord("app", out found));
            Assert.IsTrue(one.HasWord("apple", out found));
            Assert.IsTrue(one.HasWord("banana", out found));
        }

        [TestMethod]
        public void Can_get_weight()
        {
            var tree = new LcrsTrie('\0', false);
            tree.Add("pap");
            tree.Add("papp");
            tree.Add("papaya");

            Assert.AreEqual(8, tree.Weight);

            tree.Add("ape");
            tree.Add("apelsin");

            Assert.AreEqual(15, tree.Weight);
        }

        [TestMethod]
        public void Can_find_near()
        {
            var tree = new LcrsTrie('\0', false);
            var near = tree.Near("ba", 1).Select(w=>w.Value).ToList();

            Assert.IsFalse(near.Any());

            tree.Add("bad");
            near = tree.Near("ba", 1).Select(w => w.Value).ToList();

            Assert.AreEqual(1, near.Count);
            Assert.IsTrue(near.Contains("bad"));

            tree.Add("baby");
            near = tree.Near("ba", 1).Select(w => w.Value).ToList();

            Assert.AreEqual(1, near.Count);
            Assert.IsTrue(near.Contains("bad"));

            tree.Add("b");
            near = tree.Near("ba", 1).Select(w => w.Value).ToList();

            Assert.AreEqual(2, near.Count);
            Assert.IsTrue(near.Contains("bad"));
            Assert.IsTrue(near.Contains("b"));

            near = tree.Near("ba", 2).Select(w => w.Value).ToList();

            Assert.AreEqual(3, near.Count);
            Assert.IsTrue(near.Contains("b"));
            Assert.IsTrue(near.Contains("bad"));
            Assert.IsTrue(near.Contains("baby"));

            near = tree.Near("ba", 0).Select(w => w.Value).ToList();

            Assert.AreEqual(0, near.Count);

            tree.Add("bananas");
            near = tree.Near("ba", 6).Select(w => w.Value).ToList();

            Assert.AreEqual(4, near.Count);
            Assert.IsTrue(near.Contains("b"));
            Assert.IsTrue(near.Contains("bad"));
            Assert.IsTrue(near.Contains("baby"));
            Assert.IsTrue(near.Contains("bananas"));

            near = tree.Near("bazy", 1).Select(w => w.Value).ToList();

            Assert.AreEqual(1, near.Count);
            Assert.IsTrue(near.Contains("baby"));

            tree.Add("bank");
            near = tree.Near("bazy", 3).Select(w => w.Value).ToList();

            Assert.AreEqual(4, near.Count);
            Assert.IsTrue(near.Contains("baby"));
            Assert.IsTrue(near.Contains("bank"));
            Assert.IsTrue(near.Contains("bad"));
            Assert.IsTrue(near.Contains("b"));
        }

        [TestMethod]
        public void Can_find_prefixed()
        {
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

            var prefixed = tree.StartsWith("ra").Select(w=>w.Value).ToList();

            Assert.AreEqual(3, prefixed.Count);
            Assert.IsTrue(prefixed.Contains("rambo"));
            Assert.IsTrue(prefixed.Contains("raiders"));
            Assert.IsTrue(prefixed.Contains("rain"));
        }

        [TestMethod]
        public void Can_find_exact()
        {
            var tree = new LcrsTrie('\0', false);
            Word word;
            Assert.IsFalse(tree.HasWord("xxx", out word));

            tree.Add("xxx");

            Assert.IsTrue(tree.HasWord("xxx", out word));
            Assert.IsFalse(tree.HasWord("baby", out word));
            Assert.IsFalse(tree.HasWord("dad", out word));

            tree.Add("baby");

            Assert.IsTrue(tree.HasWord("xxx", out word));
            Assert.IsTrue(tree.HasWord("baby", out word));
            Assert.IsFalse(tree.HasWord("dad", out word));

            tree.Add("dad");

            Assert.IsTrue(tree.HasWord("xxx", out word));
            Assert.IsTrue(tree.HasWord("baby", out word));
            Assert.IsTrue(tree.HasWord("dad", out word));
        }

        [TestMethod]
        public void Can_build_one_leg()
        {
            var tree = new LcrsTrie('\0', false);
            Word word;
            tree.Add("baby");

            Assert.AreEqual('b', tree.LeftChild.Value);
            Assert.AreEqual('a', tree.LeftChild.LeftChild.Value);
            Assert.AreEqual('b', tree.LeftChild.LeftChild.LeftChild.Value);
            Assert.AreEqual('y', tree.LeftChild.LeftChild.LeftChild.LeftChild.Value);

            Assert.IsTrue(tree.HasWord("baby", out word));
        }

        [TestMethod]
        public void Can_build_two_legs()
        {
            var root = new LcrsTrie('\0', false);
            root.Add("baby");
            root.Add("dad");
            Word word;
            Assert.AreEqual('d', root.LeftChild.RightSibling.Value);
            Assert.AreEqual('a', root.LeftChild.LeftChild.Value);
            Assert.AreEqual('d', root.LeftChild.RightSibling.LeftChild.LeftChild.Value);

            Assert.AreEqual('b', root.LeftChild.Value);
            Assert.AreEqual('a', root.LeftChild.RightSibling.LeftChild.Value);
            Assert.AreEqual('b', root.LeftChild.LeftChild.LeftChild.Value);
            Assert.AreEqual('y', root.LeftChild.LeftChild.LeftChild.LeftChild.Value);

            Assert.IsTrue(root.HasWord("baby", out word));
            Assert.IsTrue(root.HasWord("dad", out word));
        }

        [TestMethod]
        public void Can_append()
        {
            var root = new LcrsTrie('\0', false);

            root.Add("baby");
            root.Add("bad");
            Word word;

            Assert.AreEqual('b', root.LeftChild.Value);
            Assert.AreEqual('a', root.LeftChild.LeftChild.Value);
            Assert.AreEqual('d', root.LeftChild.LeftChild.LeftChild.RightSibling.Value);

            Assert.AreEqual('b', root.LeftChild.LeftChild.LeftChild.Value);
            Assert.AreEqual('y', root.LeftChild.LeftChild.LeftChild.LeftChild.Value);

            Assert.IsTrue(root.HasWord("baby", out word));
            Assert.IsTrue(root.HasWord("bad", out word));
        }
    }
}