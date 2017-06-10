using System.Linq;
using Resin.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    [TestClass]
    public class LcrsTrieTests
    {
        [TestMethod]
        public void Can_merge_tries()
        {
            var one = new LcrsTrie('\0', false);
            one.Add("ape");
            one.Add("app");
            one.Add("bananas");

            var two = new LcrsTrie('\0', false);
            two.Add("apple");
            two.Add("banana");
            two.Add("citron");

            one.Merge(two);
            
            Assert.IsTrue(one.IsWord("ape").Any());
            Assert.IsTrue(one.IsWord("app").Any());
            Assert.IsTrue(one.IsWord("apple").Any());
            Assert.IsTrue(one.IsWord("banana").Any());
            Assert.IsTrue(one.IsWord("bananas").Any());
            Assert.IsTrue(one.IsWord("citron").Any());
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
            Assert.IsFalse(tree.IsWord("xxx").Any());

            tree.Add("xxx");

            Assert.IsTrue(tree.IsWord("xxx").Any());
            Assert.IsFalse(tree.IsWord("baby").Any());
            Assert.IsFalse(tree.IsWord("dad").Any());

            tree.Add("baby");

            Assert.IsTrue(tree.IsWord("xxx").Any());
            Assert.IsTrue(tree.IsWord("baby").Any());
            Assert.IsFalse(tree.IsWord("dad").Any());

            tree.Add("dad");

            Assert.IsTrue(tree.IsWord("xxx").Any());
            Assert.IsTrue(tree.IsWord("baby").Any());
            Assert.IsTrue(tree.IsWord("dad").Any());
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

            Assert.IsTrue(tree.IsWord("baby").Any());
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

            Assert.IsTrue(root.IsWord("baby").Any());
            Assert.IsTrue(root.IsWord("dad").Any());
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

            Assert.IsTrue(root.IsWord("baby").Any());
            Assert.IsTrue(root.IsWord("bad").Any());
        }
    }
}