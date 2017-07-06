using DocumentTable;
using StreamIndex;
using System.Collections.Generic;

namespace Resin.IO
{
    [System.Diagnostics.DebuggerDisplay("{Value} {Count}")]
    public struct Word
    {
        public readonly string Value;
        public readonly int Count;
        public readonly BlockInfo? PostingsAddress;
        public readonly IList<DocumentPosting> Postings;

        public Word(string value)
        {
            Value = value;
            Count = 1;
            PostingsAddress = null;
            Postings = null;
        }

        public Word(string value, int count, BlockInfo? postingsAddress, IList<DocumentPosting> postings = null)
        {
            Value = value;
            Count = count;
            PostingsAddress = postingsAddress;
            Postings = postings;
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