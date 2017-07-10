using System.Linq;
using Resin.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    [TestClass]
    public class AnalyzerTests : Setup
    {
        [TestMethod]
        public void Can_split_by_custom_delimiters()
        {
            var terms = new Analyzer(tokenSeparators:new []{111}).Analyze("hello world").ToList();
            Assert.AreEqual(3, terms.Count);
            Assert.AreEqual("hell", terms[0]);
            Assert.AreEqual("w", terms[1]);
            Assert.AreEqual("rld", terms[2]);
        }

        [TestMethod]
        public void Can_analyze()
        {
            var terms = new Analyzer().Analyze("Hello (World)!").ToList();
            Assert.AreEqual(2, terms.Count);
            Assert.AreEqual("hello", terms[0]);
            Assert.AreEqual("world", terms[1]);
        }

        [TestMethod]
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