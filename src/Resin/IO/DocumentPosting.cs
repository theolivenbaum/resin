using System;
using System.Collections.Generic;
using System.Linq;

namespace Resin.IO
{
    public struct DocumentPosting
    {
        public readonly int DocumentId;
        public readonly int Count;

        public DocumentPosting(int documentId, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException("count");

            DocumentId = documentId;
            Count = count;
        }

        public DocumentPosting Join(DocumentPosting other)
        {
            if (other.DocumentId != DocumentId) throw new ArgumentException();

            return new DocumentPosting(DocumentId, Count + other.Count);
        }

        public static IEnumerable<DocumentPosting> Join(IEnumerable<DocumentPosting> first, IEnumerable<DocumentPosting> other)
        {
            if (first == null) return other;

            return first.Concat(other).GroupBy(x => x.DocumentId).Select(group =>
            {
                var list = group.ToList();
                var tip = list.First();
                foreach (DocumentPosting posting in list.Skip(1))
                {
                    tip = tip.Join(posting);
                }
                return tip;
            });
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}", DocumentId, Count);
        }
    }
}