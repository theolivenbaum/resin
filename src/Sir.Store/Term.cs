using System;

namespace Sir
{
    /// <summary>
    /// A query term.
    /// </summary>
    public class Term
    {
        public IComparable Key { get; private set; }
        public AnalyzedString TokenizedString { get; private set; }
        public ulong KeyHash { get; private set; }
        public int Index { get; private set; }
        public long? KeyId { get; private set; }

        public Term(IComparable key, AnalyzedString tokenizedString, int index)
        {
            Key = key;
            KeyHash = key.ToHash();
            TokenizedString = tokenizedString;
            Index = index;
        }

        public Term(long keyId, AnalyzedString tokenizedString, int index)
        {
            KeyId = keyId;
            TokenizedString = tokenizedString;
            Index = index;
        }

        private string GetString()
        {
            var token = TokenizedString.Tokens[Index];
            return TokenizedString.Original.Substring(token.offset, token.length);
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Key, GetString());
        }
    }
}