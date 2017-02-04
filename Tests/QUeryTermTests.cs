using NUnit.Framework;
using Resin.Querying;

namespace Tests
{
    [TestFixture]
    public class QueryTermTests
    {
        [TestCase("wordssss", 0.75f, Result = 2)]
        [TestCase("wordsss", 0.75f, Result = 1)]
        [TestCase("wordss", 0.75f, Result = 1)]
        [TestCase("words", 0.75f, Result = 1)]
        [TestCase("word", 0.75f, Result = 1)]
        [TestCase("ord", 0.75f, Result = 0)]
        [TestCase("or", 0.75f, Result = 0)]
        public int Can_parse_similarity(string word, float similarity)
        {
            var q = new QueryTerm("field", word) {Similarity = similarity};

            return q.Edits;
        } 
    }
}