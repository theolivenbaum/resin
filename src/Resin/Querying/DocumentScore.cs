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
        public UInt32 DocHash { get; private set; }

        public DocumentScore(int documentId, UInt32 docHash, double score, IxInfo ix)
        {
            DocumentId = documentId;
            Score = score;
            Ix = ix;
            DocHash = docHash;
        }

        public void Combine(DocumentScore score)
        {
            if (!score.DocumentId.Equals(DocumentId)) throw new ArgumentException("Document IDs differ. Cannot combine.", "score");

            Score = (Score + score.Score);
        }

        public DocumentScore TakeLatestVersion(DocumentScore score)
        {
            if (!score.DocHash.Equals(DocHash)) throw new ArgumentException("Document hashed differ. Cannot take latest version.", "score");

            if (score.Ix.VersionId > Ix.VersionId)
            {
                return score;
            }

            return this;
        }

        public static IEnumerable<DocumentScore> Not(IEnumerable<DocumentScore> source, IEnumerable<DocumentScore> exclude)
        {
            var dic = exclude.ToDictionary(x => x.DocumentId);
            var remainder = new List<DocumentScore>();

            foreach (var score in source)
            {
                DocumentScore exists;
                if (!dic.TryGetValue(score.DocumentId, out exists))
                {
                    remainder.Add(score);
                }
            }
            return remainder;
        }

        public static IEnumerable<DocumentScore> CombineOr(IEnumerable<DocumentScore> first, IEnumerable<DocumentScore> other)
        {
            if (first == null) return other;

            return first.Concat(other).GroupBy(x => x.DocumentId).Select(group =>
            {
                var list = group.ToList();
                
                var top = list.First();
                foreach (var score in list.Skip(1))
                {
                    top.Combine(score);
                }
                return top;
            });
        }

        public static IEnumerable<DocumentScore> CombineTakingLatestVersion(IEnumerable<DocumentScore> first, IEnumerable<DocumentScore> other)
        {
            if (first == null) return other;

            return first.Concat(other).GroupBy(x => x.DocumentId).Select(group =>group.OrderBy(s=>s.Ix.VersionId).Last());
        }

        public static IEnumerable<DocumentScore> CombineAnd(IEnumerable<DocumentScore> first, IEnumerable<DocumentScore> other)
        {
            if (first == null) return other;

            var dic = other.ToDictionary(x => x.DocumentId);
            var remainder = new List<DocumentScore>();

            foreach (var score in first)
            {
                DocumentScore exists;
                if (dic.TryGetValue(score.DocumentId, out exists))
                {
                    score.Combine(exists);
                    remainder.Add(score);
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