using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Write;

namespace Tests
{
    [TestFixture]
    public class AnalyzerTests
    {
        [Test]
        public void Serializzzzzze()
        {
            using (var stream = new MemoryStream())
            {
                GraphSerializer.Serializer.Serialize(stream, new BlockInfo(0, 1188888880));
                var bytes1 = stream.ToArray();

                var bytes2 = LcrsTrieSerializer.TypeToBytes(new BlockInfo(0, 0));
            }
        }

        [Test]
        public void Stopwords()
        {
            var terms = new Analyzer(stopwords:new[]{"the", "a"}).Analyze("The americans sent us a new movie.").ToList();
            Assert.AreEqual(5, terms.Count);
            Assert.AreEqual("americans", terms[0]);
            Assert.AreEqual("sent", terms[1]);
            Assert.AreEqual("us", terms[2]);
            Assert.AreEqual("new", terms[3]);
            Assert.AreEqual("movie", terms[4]);
        }

        [Test]
        public void Separators()
        {
            var terms = new Analyzer(tokenSeparators:new []{'o'}).Analyze("hello world").ToList();
            Assert.AreEqual(3, terms.Count);
            Assert.AreEqual("hell", terms[0]);
            Assert.AreEqual("w", terms[1]);
            Assert.AreEqual("rld", terms[2]);
        }

        [Test]
        public void Can_analyze()
        {
            var terms = new Analyzer().Analyze("Hello!World?").ToList();
            Assert.AreEqual(2, terms.Count);
            Assert.AreEqual("hello", terms[0]);
            Assert.AreEqual("world", terms[1]);
        }

        [Test]
        public void Can_analyze_common()
        {
            var terms = new Analyzer().Analyze("German politician (CDU)").ToList();
            Assert.AreEqual(3, terms.Count);
            Assert.AreEqual("german", terms[0]);
            Assert.AreEqual("politician", terms[1]);
            Assert.AreEqual("cdu", terms[2]);
        }

        [Test]
        public void Can_analyze_space()
        {
            var terms = new Analyzer().Analyze("   (abc)   ").ToList();
            Assert.AreEqual(1, terms.Count);
            Assert.AreEqual("abc", terms[0]);

            terms = new Analyzer().Analyze(" ").ToList();
            Assert.AreEqual(0, terms.Count);

            terms = new Analyzer().Analyze("  ").ToList();
            Assert.AreEqual(0, terms.Count);
        }
    }
}