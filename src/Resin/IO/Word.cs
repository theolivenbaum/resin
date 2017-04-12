using System.Collections.Generic;

namespace Resin.IO
{
    public struct Word
    {
        public readonly string Value;
        public readonly BlockInfo? PostingsAddress;
        public readonly IList<DocumentPosting> Postings;

        public Word(string value, BlockInfo? postingsAddress = null, IList<DocumentPosting> postings = null)
        {
            Value = value;
            PostingsAddress = postingsAddress;
            Postings = postings;
        }

        public static Word MinValue { get { return new Word(string.Empty, BlockInfo.MinValue);} }

        public override string ToString()
        {
            return Value;
        }
    }
}