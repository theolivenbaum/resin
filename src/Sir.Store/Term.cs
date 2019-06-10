namespace Sir.Store
{
    /// <summary>
    /// A query term.
    /// </summary>
    public class Term
    {
        public object Key { get; private set; }
        public AnalyzedComputerString TokenizedString { get; private set; }
        public ulong KeyHash { get; private set; }
        public int Index { get; private set; }
        public long? KeyId { get; private set; }
        public Vector Vector { get; private set; }

        public Term(object key, AnalyzedComputerString tokenizedString, int index)
        {
            Key = key;
            KeyHash = key.ToHash();
            TokenizedString = tokenizedString;
            Index = index;
            Vector = tokenizedString.Embeddings[index];
        }

        public Term(long keyId, AnalyzedComputerString tokenizedString, int index)
        {
            KeyId = keyId;
            TokenizedString = tokenizedString;
            Index = index;
            Vector = tokenizedString.Embeddings[index];
        }

        public Term(object key, VectorNode node)
        {
            Key = key;
            KeyHash = key.ToHash();
            Vector = node.Vector;
        }

        public Term(long keyId, VectorNode node)
        {
            KeyId = keyId;
            Vector = node.Vector;
        }

        public Vector AsVector()
        {
            return Vector == null
                ? TokenizedString.Embeddings[Index]
                : Vector;
        }

        private string CreateString()
        {
            return Vector.ToString();
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", Key, CreateString());
        }
    }
}