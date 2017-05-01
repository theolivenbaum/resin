using System;
using System.Collections.Generic;
using System.Linq;

namespace Resin.IO
{
    public class DocumentPosting
    {
        public int DocumentId { get; private set; }
        public int Count { get; set; }

        public DocumentPosting(int documentId, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException("count");

            DocumentId = documentId;
            Count = count;
        }

        public DocumentPosting Join(DocumentPosting other)
        {
            if (other.DocumentId != DocumentId) throw new ArgumentException();

            Count += other.Count;

            return this;
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