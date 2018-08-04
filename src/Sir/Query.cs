namespace Sir
{
    [System.Diagnostics.DebuggerDisplay("{Term}")]
    public class Query
    {
        public ulong CollectionId { get; set; }
        public bool And { get; set; }
        public bool Or { get; set; }
        public bool Not { get; set; }
        public Term Term { get; set; }
        public Query Next { get; set; }
    }
}
