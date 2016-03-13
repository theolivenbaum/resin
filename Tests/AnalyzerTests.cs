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
    }
}