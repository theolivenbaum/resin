using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class AnalyzerTests
    {
        [Test]
        public void Can_analyze()
        {
            var terms = new Analyzer().Analyze("Hello!World?");
            Assert.AreEqual(2, terms.Length);
            Assert.AreEqual("hello", terms[0]);
            Assert.AreEqual("world", terms[1]);
        }

        [Test]
        public void Can_analyze_wierdness()
        {
            var terms = new Analyzer().Analyze("Spanish noblewoman, († 1292)");
            Assert.AreEqual(4, terms.Length);
            Assert.AreEqual("spanish", terms[0]);
            Assert.AreEqual("noblewoman", terms[1]);
            Assert.AreEqual("†", terms[2]);
            Assert.AreEqual("1292", terms[3]);
        }

        [Test]
        public void Can_analyze_common()
        {
            var terms = new Analyzer().Analyze("German politician (CDU)");
            Assert.AreEqual(3, terms.Length);
            Assert.AreEqual("german", terms[0]);
            Assert.AreEqual("politician", terms[1]);
            Assert.AreEqual("cdu", terms[2]);
        }

        [Test]
        public void Can_analyze_space()
        {
            var terms = new Analyzer().Analyze("      ");
            Assert.AreEqual(0, terms.Length);
        }
    }
}