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

        /// <summary>
        /// Scores postings that have been sorted by document ID.
        /// </summary>
        /// <param name="postings">Postings sorted by document ID</param>
        /// <returns>Unsorted scored</returns>
        protected IList<DocumentScore> Score(IList<DocumentPosting> postings)
        {
            var scoreTime = Stopwatch.StartNew();
            var scores = new List<DocumentScore>(postings.Count);

            if (postings != null && postings.Count > 0)
            {
                var docsWithTerm = postings.Count;
                var scorer = ScoringFactory.CreateScorer(Session.Version.DocumentCount, docsWithTerm);
                var docTermCount = new Dictionary<int, int>();

                foreach (var posting in postings)
                {
                    if (docTermCount.ContainsKey(posting.DocumentId))
                    {
                        docTermCount[posting.DocumentId] += 1;
                    }
                    else
                    {
                        docTermCount[posting.DocumentId] = 1;
                    }
                }

                foreach(var termCount in docTermCount)
                {
                    var docId = termCount.Key;
                    var docHash = Session.ReadDocHash(docId);

                    if (!docHash.IsObsolete)
                    {
                        var score = scorer.Score(termCount.Value);

                        scores.Add(new DocumentScore(docId, docHash.Hash, score, Session.Version));
                    }
                }
            }

            Log.DebugFormat("scored in {0}", scoreTime.Elapsed);

            return scores;
        }

        public IList<IList<DocumentPosting>> GetManyPostingsLists(IList<Term> terms)
        {
            var time = Stopwatch.StartNew();

            var addresses = new List<BlockInfo>(terms.Count);

            foreach (var term in terms)
            {
                addresses.Add(term.Word.PostingsAddress.Value);
            }

            var postings = PostingsReader.Read(addresses);

            Log.DebugFormat("fetched {0} postings lists in {1}", terms.Count, time.Elapsed);

            return postings;
        }

        protected IList<DocumentPosting> GetPostingsList(IList<Term> terms)
        {
            var postings = terms.Count > 0 ? GetManyPostingsLists(terms) : null;

            IList<DocumentPosting> result = new List<DocumentPosting>();

            if (postings != null)
            {
                foreach (var list in postings)
                foreach (var p in list)
                {
                    result.Add(p);
                }
            }

            return result;
        }

        protected IList<DocumentPosting> GetPostingsList(Term term)
        {
            return GetPostingsList(term.Word.PostingsAddress.Value);
        }

        protected IList<DocumentPosting> GetPostingsList(BlockInfo address)
        {
            var time = Stopwatch.StartNew();

            var result = PostingsReader.Read(new BlockInfo[] { address })[0];

            Log.DebugFormat("fetched 1 postings list in {0}", time.Elapsed);

            return result;
        }

        protected IList<DocumentPosting> GetSortedPostingsList(IList<BlockInfo> addresses)
        {
            var time = Stopwatch.StartNew();

            var result = new List<DocumentPosting>();
            var many = PostingsReader.Read(addresses);

            foreach(var list in many)
            {
                foreach (var posting in list)
                {
                    result.Add(posting);
                }
            }
            result.Sort(new DocumentPostingComparer());

            Log.DebugFormat("fetched {0} sorted postings lists in {1}", addresses.Count, time.Elapsed);

            return result;
        }
    }
}
