namespace Sir
{
    [System.Diagnostics.DebuggerDisplay("{Term}")]
    public class Query
    {
        public ulong CollectionId { get; set; }
        public Query And { get; set; }
        public Query Or { get; set; }
        public Query Not { get; set; }
        public Term Term { get; set; }
    }
}
