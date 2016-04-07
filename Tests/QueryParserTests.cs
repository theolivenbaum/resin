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
            var q = new QueryParser(new Analyzer()).Parse("title:rambo");

            Assert.AreEqual("+title:rambo", q.ToString());
        }

        [Test]
        public void Can_parse_two_terms_as_AND()
        {
            var q = new QueryParser(new Analyzer()).Parse("title:first +title:blood");

            Assert.AreEqual("+title:first +title:blood", q.ToString());
        }

        [Test]
        public void Can_parse_two_terms_as_OR()
        {
            var q = new QueryParser(new Analyzer()).Parse("title:first title:blood");

            Assert.AreEqual("+title:first title:blood", q.ToString());
        }

        [Test]
        public void Can_parse_two_terms_as_NOT()
        {
            var q = new QueryParser(new Analyzer()).Parse("title:first -title:blood");

            Assert.AreEqual("+title:first -title:blood", q.ToString());
        }

        [Test]
        public void Can_parse_three_terms_as_AND()
        {
            var q = new QueryParser(new Analyzer()).Parse("title:first +title:blood +genre:action");

            Assert.AreEqual("+title:first +title:blood +genre:action", q.ToString());
        }

        [Test]
        public void Can_parse()
        {
            var q = new QueryParser(new Analyzer()).Parse("title:first* -title:blood~ genre:action*");

            Assert.AreEqual("+title:first* -title:blood~ genre:action*", q.ToString());
        }

        [Test]
        public void Can_parse_prefix_term()
        {
            var q = new QueryParser(new Analyzer()).Parse("title:ram*");

            Assert.AreEqual("+title:ram*", q.ToString());
        }

        [Test]
        public void Can_parse_fuzzy_term()
        {
            var q = new QueryParser(new Analyzer()).Parse("title:raymbo~");

            Assert.AreEqual("+title:raymbo~", q.ToString());
        }
    }
}