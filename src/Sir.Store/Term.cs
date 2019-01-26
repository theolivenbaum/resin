using System;

namespace Sir
{
    /// <summary>
    /// A query term.
    /// </summary>
    public class Term
    {
        public IComparable Key { get; private set; }
        public AnalyzedString TokenizedString { get; set; }
        public long KeyId { get; set; }
        public int Index { get; set; }

        public string GetString()
        {
            var token = TokenizedString.Tokens[Index];
            return TokenizedString.Original.Substring(token.offset, token.length);
        }

        public Term(IComparable key, AnalyzedString tokenizedString, int index)
        {
            Key = key;
            TokenizedString = tokenizedString;
            Index = index;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Key, GetString());
        }
    }
}