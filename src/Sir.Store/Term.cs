using Sir.Store;
using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// A query term.
    /// </summary>
    public class Term
    {
        public object Key { get; private set; }
        public AnalyzedString TokenizedString { get; private set; }
        public ulong KeyHash { get; private set; }
        public int Index { get; private set; }
        public long? KeyId { get; private set; }
        public VectorNode Node { get; private set; }

        public Term(object key, AnalyzedString tokenizedString, int index)
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

        public Term(object key, VectorNode node)
        {
            Key = key;
            KeyHash = key.ToHash();
            Node = node;
        }

        public Term(long keyId, VectorNode node)
        {
            KeyId = keyId;
            Node = node;
        }

        public SortedList<long, int> AsVector()
        {
            return Node == null
                ? TokenizedString.Embeddings[Index]
                : Node.Vector;
        }

        private string GetDebugString()
        {
            if (Node != null)
                return Node.ToString();

            var token = TokenizedString.Tokens[Index];
            return TokenizedString.Original.Substring(token.offset, token.length);
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Key, GetDebugString());
        }
    }
}