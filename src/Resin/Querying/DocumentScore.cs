using System;
using System.Collections.Generic;
using System.Linq;
using Resin.IO;

namespace Resin.Querying
{
    public class DocumentScore
    {
        public int DocumentId { get; private set; }
        public double Score { get; private set; }
        public IxInfo Ix { get; private set; }

        public DocumentScore(int documentId, double score, IxInfo ix)
        {
            DocumentId = documentId;
            Score = score;
            Ix = ix;
        }

        public void Join(DocumentScore score)
        {
            if (!score.DocumentId.Equals(DocumentId)) throw new ArgumentException("Document IDs differ. Cannot add.", "score");

            Score = (Score + score.Score);
        }

        public static IEnumerable<DocumentScore> CombineOr(IEnumerable<DocumentScore> first, IEnumerable<DocumentScore> other)
        {
            if (first == null) return other;

            return first.Concat(other).GroupBy(x => x.DocumentId).Select(group =>
            {
                var list = group.ToList();
                
                var top = list.First();
                foreach (var posting in list.Skip(1))
                {
                    top.Join(posting);
                }
                return top;
            });
        }

        public static IEnumerable<DocumentScore> CombineAnd(IEnumerable<DocumentScore> first, IEnumerable<DocumentScore> other)
        {
            if (first == null) return other;

            var dic = other.ToDictionary(x => x.DocumentId);
            var remainder = new List<DocumentScore>();

            foreach (var posting in first)
            {
                DocumentScore exists;
                if (dic.TryGetValue(posting.DocumentId, out exists))
                {
                    posting.Join(exists);
                    remainder.Add(posting);
                }
            }
            return remainder;
        }

        public override string ToString()
        {
            return string.Format("docid:{0} score:{1}", DocumentId, Score);
        }
    }
}