namespace Sir.Store
{
    public struct DocumentTerm
    {
        public long DocumentId { get; }
        public long KeyId { get; }
        public byte[] Value { get; }
        public byte DataType { get; }

        public DocumentTerm(long documentId, long keyId, byte[] value, byte dataType)
        {
            DocumentId = documentId;
            KeyId = keyId;
            Value = value;
            DataType = dataType;
        }
    }
}