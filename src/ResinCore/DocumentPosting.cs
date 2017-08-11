using System.Diagnostics;

namespace Resin
{
    [DebuggerDisplay("{DocumentId}:{Position}")]
    public struct DocumentPosting
    {
        public int DocumentId { get; private set; }
        public int Position { get; set; }
        public bool HasValue { get; set; }

        public DocumentPosting(int documentId, int position)
        {
            DocumentId = documentId;
            Position = position;
            HasValue = true;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", DocumentId, Position);
        }
    }
}