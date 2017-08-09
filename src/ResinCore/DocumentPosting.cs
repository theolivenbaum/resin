using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Resin
{
    [DebuggerDisplay("{DocumentId}:{Position}")]
    public class DocumentPosting
    {
        public int DocumentId { get; private set; }
        public int Position { get; set; }
        public DocumentPosting Next { get; set; }

        public DocumentPosting(int documentId, int position)
        {
            DocumentId = documentId;
            Position = position;
        }

        public void Add(DocumentPosting other)
        {
            if (other.DocumentId != DocumentId) throw new ArgumentException("other");

            Position += other.Position;
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

    public static class DocumentPostingExtensions
    {
        public static IList<DocumentPosting> Sum(this IList<IList<DocumentPosting>> source)
        {
            if (source.Count == 0) return new List<DocumentPosting>();

            if (source.Count == 1) return Sum(source[0]);

            var first = source[0];

            for (var i = 1;i<source.Count; i++)
            {
                first = Sum(first, source[i]);
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