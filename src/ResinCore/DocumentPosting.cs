using System.Collections.Generic;
using System.Diagnostics;

namespace Resin
{
    [DebuggerDisplay("{DocumentId}:{Data}")]
    public struct DocumentPosting
    {
        public int DocumentId { get; private set; }
        public int Data { get; set; }
        public bool HasValue { get; set; }

        public DocumentPosting(int documentId, int data)
        {
            DocumentId = documentId;
            Data = data;
            HasValue = true;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", DocumentId, Data);
        }
    }

    public class DocumentPostingComparer : IComparer<DocumentPosting>
    {
        public int Compare(DocumentPosting x, DocumentPosting y)
        {
            if (x.DocumentId < y.DocumentId) return -1;
            if (x.DocumentId > y.DocumentId) return 1;

            if (x.Data < y.Data) return -1;
            if (x.Data > y.Data) return 1;

            return 0;
        }

        private int Compare(double x, double y)
        {
            if (x < y) return 1;
            if (x > y) return -1;
            return 0;
        }
    }
}