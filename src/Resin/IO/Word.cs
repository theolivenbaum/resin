using System.Collections.Generic;

namespace Resin.IO
{
    [System.Diagnostics.DebuggerDisplay("{ToString()}")]
    public struct Word
    {
        public readonly string Value;
        public readonly int Count;
        public readonly BlockInfo? PostingsAddress;
        public readonly IList<DocumentPosting> Postings;

        public Word(string value, int count = 1, BlockInfo? postingsAddress = null, IList<DocumentPosting> postings = null)
        {
            Value = value;
            Count = count;
            PostingsAddress = postingsAddress;
            Postings = postings;
        }

        public static Word MinValue { get { return new Word(string.Empty);} }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Value, Count);
        }
    }
}