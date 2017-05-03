using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Resin.IO
{
    [DebuggerDisplay("{DocumentId}:{Count}")]
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

        public void Add(DocumentPosting other)
        {
            if (other.DocumentId != DocumentId) throw new ArgumentException("other");

            Count += other.Count;
        }
    }

    public static class DocumentPostingExtensions
    {
        public static IEnumerable<DocumentPosting> Sum(this IList<IList<DocumentPosting>> source)
        {
            if (source.Count == 0) return new List<DocumentPosting>();

            if (source.Count == 1) return source[0];

            var first = source[0];

            foreach(var list in source.Skip(1))
            {
                first = Sum(first, list).ToList();
            }

            return first;
        }

        public static IEnumerable<DocumentPosting> Sum(IEnumerable<DocumentPosting> first, IEnumerable<DocumentPosting> other)
        {
            return first.Concat(other).GroupBy(x => x.DocumentId).Select(group =>
            {
                var list = group.ToList();
                var tip = list.First();

                foreach (DocumentPosting posting in list.Skip(1))
                {
                    tip.Add(posting);
                }
                return tip;
            });
        }
    }
}