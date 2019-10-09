namespace Sir.VectorSpace
{
    public class Term
    {
        public string Key { get; private set; }
        public AnalyzedData TokenizedString { get; private set; }
        public ulong KeyHash { get; private set; }
        public int Index { get; private set; }
        public long? KeyId { get; private set; }
        public IVector Vector { get; private set; }

        public Term(string key, AnalyzedData tokenizedString, int index)
        {
            Key = key;
            KeyHash = key.ToHash();
            TokenizedString = tokenizedString;
            Index = index;
            Vector = tokenizedString.Embeddings[index];
        }

        public Term(string key, VectorNode node)
        {
            Key = key;
            KeyHash = key.ToHash();
            Vector = node.Vector;
        }

        public IVector AsVector()
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