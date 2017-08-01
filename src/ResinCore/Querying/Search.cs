using DocumentTable;
using log4net;
using Resin.IO;
using Resin.IO.Read;
using StreamIndex;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

        protected IList<DocumentScore> Score(IList<DocumentPosting> postings)
        {
            var scoreTime = Stopwatch.StartNew();
            var scores = new List<DocumentScore>(postings.Count);

            if (postings != null && postings.Count > 0)
            {
                var docsWithTerm = postings.Count;
                var scorer = ScoringFactory.CreateScorer(Session.Version.DocumentCount, docsWithTerm);
                var postingsByDoc = postings.GroupBy(p => p.DocumentId);

                foreach (var posting in postingsByDoc)
                {
                    var docId = posting.Key;
                    var docHash = Session.ReadDocHash(docId);

                    if (!docHash.IsObsolete)
                    {
                        var score = scorer.Score(posting.Count());

                        scores.Add(new DocumentScore(docId, docHash.Hash, score, Session.Version));
                    }
                }
            }

            Log.DebugFormat("scored in {0}", scoreTime.Elapsed);

            return scores;
        }

        public IList<IList<DocumentPosting>> ReadPostings(IList<Term> terms)
        {
            var time = Stopwatch.StartNew();

            var addresses = new List<BlockInfo>(terms.Count);

            foreach (var term in terms)
            {
                addresses.Add(term.Word.PostingsAddress.Value);
            }

            var postings = PostingsReader.Read(addresses);

            Log.DebugFormat("read postings in {0}", time.Elapsed);

            return postings;
        }

        protected IList<DocumentPosting> GetPostings(IList<Term> terms)
        {
            var postings = terms.Count > 0 ? ReadPostings(terms) : null;

            IList<DocumentPosting> reduced;

            if (postings == null)
            {
                reduced = new DocumentPosting[0];
            }
            else
            {
                reduced = postings.Sum();
            }

            return reduced;
        }
    }
}
