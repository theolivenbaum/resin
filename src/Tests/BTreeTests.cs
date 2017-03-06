using System.IO;
using CSharpTest.Net.Collections;
using CSharpTest.Net.Synchronization;
using NUnit.Framework;
using Resin;
using Resin.IO;

namespace Tests
{
    [TestFixture]
    public class BTreeTests
    {
        [Test]
        public void Can_lookup()
        {
            var writeOptions = new BPlusTree<Term, DocumentPosting[]>.OptionsV2(
                new TermSerializer(), 
                new ArraySerializer<DocumentPosting>(new PostingSerializer()));

            writeOptions.FileName = Path.Combine(Setup.Dir, string.Format("{0}-{1}.{2}", "Can_lookup", "db", "bpt"));
            writeOptions.CreateFile = CreatePolicy.Always;

            var write = new BPlusTree<Term, DocumentPosting[]>(writeOptions);

            write.Add(new Term("title", new Word("bad")), new []{new DocumentPosting(1, 1) });
            write.Add(new Term("title", new Word("blood")), new[] { new DocumentPosting(1, 1) });
            write.Add(new Term("description", new Word("ape")), new[] { new DocumentPosting(1, 1) });

            write.Dispose();

            var readOptions = new BPlusTree<Term, DocumentPosting[]>.OptionsV2(
                new TermSerializer(),
                new ArraySerializer<DocumentPosting>(new PostingSerializer()));

            readOptions.FileName = Path.Combine(Setup.Dir, string.Format("{0}-{1}.{2}", "Can_lookup", "db", "bpt"));
            readOptions.ReadOnly = true;
            readOptions.LockingFactory = new IgnoreLockFactory();

            var read = new BPlusTree<Term, DocumentPosting[]>(readOptions);

            Assert.IsTrue(read.ContainsKey(new Term("title", new Word("bad"))));
            Assert.IsTrue(read.ContainsKey(new Term("title", new Word("blood"))));
            Assert.IsFalse(read.ContainsKey(new Term("description", new Word("blood"))));
            Assert.IsTrue(read.ContainsKey(new Term("description", new Word("ape"))));

            var postings = read[new Term("title", new Word("bad"))];

            Assert.That(postings[0].DocumentId, Is.EqualTo(1));
        }

        [Test]
        public void Can_equate()
        {
            var t1 = new Term("title", new Word("rambo"));

            Assert.IsTrue(t1.Equals(t1));
            Assert.IsTrue(t1.Equals(new Term("title", new Word("rambo"))));
        }
    }
}