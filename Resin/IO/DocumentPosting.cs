using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Resin.IO
{
    [Serializable]
    public class DocumentPosting
    {
        public string DocumentId { get; private set; }
        public int Count { get; private set; }

        public Term Term { get; set; }

        public DocumentPosting(string documentId, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException("count");

            DocumentId = documentId;
            Count = count;
        }

        public DocumentPosting Combine(DocumentPosting other)
        {
            if (other.DocumentId!=DocumentId) throw new ArgumentException("Doc IDs do not match", "other");

            return new DocumentPosting(DocumentId, Count + other.Count){Term = Term};
        }
        
        public override string ToString()
        {
            return string.Format("{0}:{1}", DocumentId, Count);
        }

        public static IEnumerable<DocumentPosting> JoinOr(IEnumerable<DocumentPosting> first, IEnumerable<DocumentPosting> second)
        {
            var joined = new ConcurrentDictionary<string, DocumentPosting>(first.ToDictionary(x => x.DocumentId, x => x));

            foreach (var item in second)
            {
                joined.AddOrUpdate(item.DocumentId, item, (s, posting) => posting.Combine(item));
            }

            return joined.Values;
        }

        public static IEnumerable<DocumentPosting> JoinAnd(IEnumerable<DocumentPosting> first, IEnumerable<DocumentPosting> second)
        {
            var joined = new ConcurrentDictionary<string, DocumentPosting>(first.ToDictionary(x => x.DocumentId, x => x));
            var other = second.ToDictionary(x => x.DocumentId, x => x);

            foreach (var item in joined.Values)
            {
                DocumentPosting existing;

                if (other.TryGetValue(item.DocumentId, out existing))
                {
                    joined.AddOrUpdate(item.DocumentId, item, (s, posting) => posting.Combine(item));
                }
                else
                {
                    DocumentPosting removed;
                    joined.TryRemove(item.DocumentId, out removed);
                }
            }

            return joined.Values;
        }

        public static IEnumerable<DocumentPosting> JoinNot(IEnumerable<DocumentPosting> first, IEnumerable<DocumentPosting> second)
        {
            var joined = new ConcurrentDictionary<string, DocumentPosting>(first.ToDictionary(x => x.DocumentId, x => x));
            var other = second.ToDictionary(x => x.DocumentId, x => x);

            foreach (var item in other)
            {
                DocumentPosting removed;
                joined.TryRemove(item.Key, out removed);
            }

            return joined.Values;
        } 

    }
}