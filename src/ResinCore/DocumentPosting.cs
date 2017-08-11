using System.Diagnostics;

namespace Resin
{
    [DebuggerDisplay("{DocumentId}:{Position}")]
    public class DocumentPosting
    {
        public int DocumentId { get; private set; }
        public int Position { get; set; }

        public DocumentPosting(int documentId, int position)
        {
            DocumentId = documentId;
            Position = position;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", DocumentId, Position);
        }
    }

    [DebuggerDisplay("{DocumentId}:{Position}")]
    public struct Posting
    {
        public int DocumentId { get; private set; }
        public int Position { get; set; }

        public Posting(int documentId, int position)
        {
            DocumentId = documentId;
            Position = position;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", DocumentId, Position);
        }
    }
}