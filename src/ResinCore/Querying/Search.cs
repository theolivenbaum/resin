using DocumentTable;
using log4net;
using Resin.IO;
using Resin.IO.Read;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Resin.Querying
{
    public abstract class Search
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(Search));

        protected IReadSession Session { get; set; }
        protected IScoringSchemeFactory ScoringFactory { get; set; }
        protected PostingsReader PostingsReader { get; set; }

        protected Search(
            IReadSession session, IScoringSchemeFactory scoringFactory, PostingsReader postingsReader)
        {
            Session = session;
            ScoringFactory = scoringFactory;
            PostingsReader = postingsReader;
        }

        protected ITrieReader GetTreeReader(string field)
        {
            var key = field.ToHash();
            long offset;

            if (Session.Version.FieldOffsets.TryGetValue(key, out offset))
            {
                Session.Stream.Seek(offset, SeekOrigin.Begin);
                return new MappedTrieReader(Session.Stream);
            }
            return null;
        }

        /// <summary>
        /// Scores postings that have been sorted by document ID.
        /// </summary>
        /// <param name="postings">Postings sorted by document ID</param>
        /// <returns>Unsorted scored</returns>
        protected IList<DocumentScore> Score(IList<DocumentPosting> postings)
        {
            var scoreTime = Stopwatch.StartNew();
            var scores = new List<DocumentScore>(postings.Count);
            var docsWithTerm = postings.Count;
            var scorer = ScoringFactory.CreateScorer(Session.Version.DocumentCount, docsWithTerm);

            foreach (var termCount in postings)
            {
                var docHash = Session.ReadDocHash(termCount.DocumentId);

                if (!docHash.IsObsolete)
                {
                    var score = scorer.Score(termCount.Data);

                    scores.Add(
                        new DocumentScore(termCount.DocumentId, docHash.Hash, score, Session.Version));
                }
            }

            Log.DebugFormat("scored in {0}", scoreTime.Elapsed);

            return scores;
        }
    }
}
