using System.Linq;
using NUnit.Framework;
using Resin;

namespace Tests
{
    [TestFixture]
    public class QueryParserTests
    {
        [Test]
        public void Can_parse_one_term()
        {
            var terms = new QueryParser(new Analyzer()).Parse("title:rambo").ToList();
            
            Assert.AreEqual(1, terms.Count);

            Assert.AreEqual("title", terms[0].Field);
            Assert.AreEqual("rambo", terms[0].Token);

            Assert.IsFalse(terms[0].Fuzzy);
            Assert.IsFalse(terms[0].Prefix);

            Assert.IsTrue(terms[0].And);
            Assert.IsFalse(terms[0].Not);
        }

        [Test]
        public void Can_parse_two_terms_as_AND()
        {
            var terms = new QueryParser(new Analyzer()).Parse("title:first +title:blood").ToList();

            Assert.AreEqual(2, terms.Count);

            Assert.AreEqual("title", terms[1].Field);
            Assert.AreEqual("blood", terms[1].Token);

            Assert.IsFalse(terms[1].Fuzzy);
            Assert.IsFalse(terms[1].Prefix);

            Assert.IsTrue(terms[1].And);
            Assert.IsFalse(terms[0].Not);
        }

        [Test]
        public void Can_parse_two_terms_as_OR()
        {
            var terms = new QueryParser(new Analyzer()).Parse("title:first title:blood").ToList();

            Assert.AreEqual(2, terms.Count);

            Assert.AreEqual("title", terms[1].Field);
            Assert.AreEqual("blood", terms[1].Token);

            Assert.IsFalse(terms[1].Fuzzy);
            Assert.IsFalse(terms[1].Prefix);

            Assert.IsFalse(terms[1].And);
            Assert.IsFalse(terms[0].Not);
        }

        [Test]
        public void Can_parse_two_terms_as_NOT()
        {
            var terms = new QueryParser(new Analyzer()).Parse("title:first -title:blood").ToList();

            Assert.AreEqual(2, terms.Count);

            Assert.AreEqual("title", terms[1].Field);
            Assert.AreEqual("blood", terms[1].Token);

            Assert.IsFalse(terms[1].Fuzzy);
            Assert.IsFalse(terms[1].Prefix);

            Assert.IsFalse(terms[1].And);
            Assert.IsTrue(terms[1].Not);
        }
        
        [Test]
        public void Can_parse_prefix_term()
        {
            var terms = new QueryParser(new Analyzer()).Parse("title:ram*").ToList();

            Assert.AreEqual("title", terms[0].Field);
            Assert.AreEqual("ram", terms[0].Token);

            Assert.IsFalse(terms[0].Fuzzy);
            Assert.IsTrue(terms[0].Prefix);
        }

        [Test]
        public void Can_parse_fuzzy_term()
        {
            var terms = new QueryParser(new Analyzer()).Parse("title:raymbo~").ToList();

            Assert.AreEqual("title", terms[0].Field);
            Assert.AreEqual("raymbo", terms[0].Token);

            Assert.IsTrue(terms[0].Fuzzy);
            Assert.IsFalse(terms[0].Prefix);
        } 
    }
}