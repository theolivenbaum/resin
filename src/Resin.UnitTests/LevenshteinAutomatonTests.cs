using Resin.Analysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    [TestClass]
    public class LevenshteinAutomatonTests : Setup
    {
        [TestMethod]
        public void Can_calculate_distance()
        {
            var query = "rambo";
            var auto = new LevenshteinAutomaton(query, 1);

            Assert.IsTrue(auto.IsValid('r', 0));
            Assert.IsTrue(auto.IsValid('a', 1));
            Assert.IsTrue(auto.IsValid('m', 2));
            Assert.IsTrue(auto.IsValid('b', 3));
            Assert.IsTrue(auto.IsValid('o', 4));

            //Assert.IsTrue(auto.IsValid('x', 0));
            //Assert.IsTrue(auto.IsValid('a', 1));
            //Assert.IsTrue(auto.IsValid('m', 2));
            //Assert.IsTrue(auto.IsValid('b', 3));
            //Assert.IsTrue(auto.IsValid('o', 4));

            //Assert.IsTrue(auto.IsValid('x', 0));
            //Assert.IsTrue(auto.IsValid('a', 1));
            //Assert.IsTrue(auto.IsValid('m', 2));
            //Assert.IsTrue(auto.IsValid('b', 3));
            //Assert.IsTrue(auto.IsValid('o', 4));
            //Assert.IsFalse(auto.IsValid('x', 5));

            //Assert.IsTrue(auto.IsValid('r', 0));
            //Assert.IsTrue(auto.IsValid('a', 1));
            //Assert.IsTrue(auto.IsValid('m', 2));
            //Assert.IsTrue(auto.IsValid('b', 3));
            //Assert.IsTrue(auto.IsValid('o', 4));
            //Assert.IsTrue(auto.IsValid('x', 5));

            //Assert.IsTrue(auto.IsValid('r', 0));
            //Assert.IsTrue(auto.IsValid('a', 1));
            //Assert.IsTrue(auto.IsValid('m', 2));
            //Assert.IsTrue(auto.IsValid('b', 3));
            //Assert.IsTrue(auto.IsValid('o', 4));
            //Assert.IsTrue(auto.IsValid('x', 5));
            //Assert.IsFalse(auto.IsValid('x', 6));
        }
    }
}