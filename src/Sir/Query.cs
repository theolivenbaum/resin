namespace Sir
{
    public class Query
    {
        public ulong CollectionId { get; set; }
        public bool And { get; set; }
        public bool Or { get; set; }
        public bool Not { get; set; }
        public Term Term { get; set; }
        public Query Next { get; set; }

        public override string ToString()
        {
            var op = And ? "+" : Or ? " " : "-";
            return string.Format("{0}{1}", op, Term);
        }
    }
}
