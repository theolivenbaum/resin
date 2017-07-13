using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DocumentTable
{
    [DebuggerDisplay("{DocumentId}:{Count}")]
    public struct DocumentPosting
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
        public static IList<DocumentPosting> Sum(this IList<IList<DocumentPosting>> source)
        {
            if (source.Count == 0) return new List<DocumentPosting>();

            if (source.Count == 1) return Sum(source[0]);

            var first = source[0];

            foreach(var list in source.Skip(1))
            {
                first = Sum(first, list);
            }

            return first;
        }

        public static IList<DocumentPosting> Sum(IEnumerable<DocumentPosting> first, IEnumerable<DocumentPosting> other)
        {
            return first.Concat(other).GroupBy(x => x.DocumentId).Select(group =>
            {
                var list = group.ToArray();
                var tip = list[0];

                for (int index = 1; index < list.Length; index++)
                {
                    tip.Add(list[index]);
                }

                return tip;
            }).ToList();
        }

        public static IList<DocumentPosting> Sum(IList<DocumentPosting> postings)
        {
            return postings.GroupBy(x => x.DocumentId).Select(group =>
            {
                var list = group.ToArray();
                var tip = list[0];

                for (int index = 1; index < list.Length; index++)
                {
                    tip.Add(list[index]);
                }
                return tip;
            }).ToList();
        }
    }
}