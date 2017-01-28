using System.IO;
using System.Text;
using NUnit.Framework;
using Resin.IO;

namespace Tests
{
    [TestFixture]
    public class BinaryTreeTests
    {
        [Ignore]
        public void Can_scan_from_disk()
        {
            var tree = new BinaryTree('\0', false);
            tree.Add("baby");
            tree.Add("bad");
            tree.Add("bank");
            tree.Add("box");
            tree.Add("dad");
            tree.Add("daddy");
            tree.Add("dance");
            tree.Add("dancing");

            tree.Serialize("0.bt");
        }

        [Test]
        public void Can_serialize()
        {
            var tree = new BinaryTree('\0', false);
            tree.Add("baby");
            tree.Add("bad");
            tree.Add("bank");
            tree.Add("box");
            tree.Add("dad");
            tree.Add("daddy");
            tree.Add("dance");
            tree.Add("dancing");

            tree.Serialize("0.bt");
            var acctual = File.ReadAllText("0.bt", Encoding.Unicode);

            const string expected = "d10\r\na00\r\nn10\r\nc00\r\ni10\r\nn00\r\ng01\r\ne01\r\nd00\r\nd00\r\ny01\r\nb00\r\no10\r\nx01\r\na00\r\nn10\r\nk01\r\nd11\r\nb00\r\ny01\r\n";

            Assert.AreEqual(expected, acctual);
        }

        [Test]
        public void Can_find_near()
        {
            var tree = new BinaryTree('\0', false);
            var near = tree.Near("ba", 1);

            Assert.That(near, Is.Empty);

            tree.Add("bad");
            near = tree.Near("ba", 1);

            Assert.That(near.Count, Is.EqualTo(1));
            Assert.IsTrue(near.Contains("bad"));

            tree.Add("baby");
            near = tree.Near("ba", 1);

            Assert.That(near.Count, Is.EqualTo(1));
            Assert.IsTrue(near.Contains("bad"));

            tree.Add("b");
            near = tree.Near("ba", 1);

            Assert.That(near.Count, Is.EqualTo(2));
            Assert.IsTrue(near.Contains("bad"));
            Assert.IsTrue(near.Contains("b"));

            near = tree.Near("ba", 2);

            Assert.That(near.Count, Is.EqualTo(3));
            Assert.IsTrue(near.Contains("b"));
            Assert.IsTrue(near.Contains("bad"));
            Assert.IsTrue(near.Contains("baby"));

            near = tree.Near("ba", 0);

            Assert.That(near.Count, Is.EqualTo(0));

            tree.Add("bananas");
            near = tree.Near("ba", 6);

            Assert.That(near.Count, Is.EqualTo(4));
            Assert.IsTrue(near.Contains("b"));
            Assert.IsTrue(near.Contains("bad"));
            Assert.IsTrue(near.Contains("baby"));
            Assert.IsTrue(near.Contains("bananas"));
        }

        [Test]
        public void Can_find_suffixes()
        {
            var tree = new BinaryTree('\0', false);

            var prefixed = tree.StartsWith("ba");

            Assert.That(prefixed, Is.Empty);

            tree.Add("bad");

            prefixed = tree.StartsWith("ba");

            Assert.That(prefixed.Count, Is.EqualTo(1));
            Assert.IsTrue(prefixed.Contains("bad"));

            tree.Add("baby");

            prefixed = tree.StartsWith("ba");

            Assert.That(prefixed.Count, Is.EqualTo(2));
            Assert.IsTrue(prefixed.Contains("bad"));
            Assert.IsTrue(prefixed.Contains("baby"));
        }

        [Test]
        public void Can_find_exact()
        {
            var tree = new BinaryTree('\0', false);

            Assert.False(tree.HasWord("xxx"));

            tree.Add("xxx");

            Assert.True(tree.HasWord("xxx"));
            Assert.False(tree.HasWord("baby"));
            Assert.False(tree.HasWord("dad"));

            tree.Add("baby");

            Assert.True(tree.HasWord("xxx"));
            Assert.True(tree.HasWord("baby"));
            Assert.False(tree.HasWord("dad"));

            tree.Add("dad");

            Assert.True(tree.HasWord("xxx"));
            Assert.True(tree.HasWord("baby"));
            Assert.True(tree.HasWord("dad"));
        }

        [Test]
        public void Can_build_one_leg()
        {
            var tree = new BinaryTree('\0', false);
            
            tree.Add("baby");

            Assert.That(tree.LeftChild.Value, Is.EqualTo('b'));
            Assert.That(tree.LeftChild.LeftChild.Value, Is.EqualTo('a'));
            Assert.That(tree.LeftChild.LeftChild.LeftChild.Value, Is.EqualTo('b'));
            Assert.That(tree.LeftChild.LeftChild.LeftChild.LeftChild.Value, Is.EqualTo('y'));

            Assert.True(tree.HasWord("baby"));
        }

        [Test]
        public void Can_build_two_legs()
        {
            var root = new BinaryTree('\0', false);

            root.Add("baby");
            root.Add("dad");

            Assert.That(root.LeftChild.Value, Is.EqualTo('d'));
            Assert.That(root.LeftChild.LeftChild.Value, Is.EqualTo('a'));
            Assert.That(root.LeftChild.LeftChild.LeftChild.Value, Is.EqualTo('d'));

            Assert.That(root.LeftChild.RightSibling.Value, Is.EqualTo('b'));
            Assert.That(root.LeftChild.RightSibling.LeftChild.Value, Is.EqualTo('a'));
            Assert.That(root.LeftChild.RightSibling.LeftChild.LeftChild.Value, Is.EqualTo('b'));
            Assert.That(root.LeftChild.RightSibling.LeftChild.LeftChild.LeftChild.Value, Is.EqualTo('y'));

            Assert.True(root.HasWord("baby"));
            Assert.True(root.HasWord("dad"));
        }

        [Test]
        public void Can_append()
        {
            var root = new BinaryTree('\0', false);

            root.Add("baby");
            root.Add("bad");

            Assert.That(root.LeftChild.Value, Is.EqualTo('b'));
            Assert.That(root.LeftChild.LeftChild.Value, Is.EqualTo('a'));
            Assert.That(root.LeftChild.LeftChild.LeftChild.Value, Is.EqualTo('d'));

            Assert.That(root.LeftChild.LeftChild.LeftChild.RightSibling.Value, Is.EqualTo('b'));
            Assert.That(root.LeftChild.LeftChild.LeftChild.RightSibling.LeftChild.Value, Is.EqualTo('y'));

            Assert.True(root.HasWord("baby"));
            Assert.True(root.HasWord("bad"));
        }
    }
}