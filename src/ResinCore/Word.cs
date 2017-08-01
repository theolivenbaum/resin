using StreamIndex;
using System.Collections.Generic;

namespace Resin
{
    [System.Diagnostics.DebuggerDisplay("{Value}")]
    public class Word
    {
        public readonly string Value;
        public readonly BlockInfo? PostingsAddress;

        public Word(string value)
        {
            Value = value;
            PostingsAddress = null;
        }

        public Word(string value, BlockInfo? postingsAddress)
        {
            Value = value;
            PostingsAddress = postingsAddress;
        }

        public static Word MinValue { get { return new Word(string.Empty);} }

        public override string ToString()
        {
            return Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    public static class WordExtensions
    {
        public static IList<Term> ToTerms(this IList<Word> words, string field)
        {
            var terms = new List<Term>(words.Count);
            foreach (var word in words)
            {
                terms.Add(new Term(field, word));
            }
            return terms;
        }
    }
}