using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using log4net;
using Resin.IO.Read;
using Resin.Querying;
using Resin.IO;
using DocumentTable;
using Resin.Analysis;

namespace Resin
{
    public class Collector : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly string _directory;
        private readonly IScoringSchemeFactory _scorerFactory;
        private readonly IDictionary<Query, IList<DocumentScore>> _scoreCache;
        private readonly IReadSession _readSession;

        public Collector(string directory, IReadSession readSession, IScoringSchemeFactory scorerFactory = null)
        {
            _readSession = readSession;
            _directory = directory;
            _scorerFactory = scorerFactory??new TfIdfFactory();
            _scoreCache = new Dictionary<Query, IList<DocumentScore>>();
        }

        public DocumentScore[] Collect(IList<QueryContext> query)
        {
            foreach (var clause in query)
            {
                Scan(clause);
            }

            var reduceTime = Stopwatch.StartNew();
            var reduced = query.Reduce().ToArray();

            Log.DebugFormat("reduced query {0} producing {1} scores in {2}", query.ToQueryString(), reduced.Length, reduceTime.Elapsed);

            return reduced;
        }

        private void Scan(QueryContext ctx)
        {
            var time = new Stopwatch();
            time.Start();

            if (ctx.Query is TermQuery)
            {
                TermScan(ctx);
            }
            else if (ctx.Query is PhraseQuery)
            {
                PhraseScan(ctx);
            }
            else
            {
                RangeScan(ctx);
            }

            Log.DebugFormat("scanned {0} in {1}", ctx.Query.Serialize(), time.Elapsed);
        }

        private void TermScan(QueryContext ctx)
        {
            IList<Term> terms;

            using (var reader = GetTreeReader(ctx.Query.Field))
            {
                if (ctx.Query.Fuzzy)
                {
                    terms = reader.SemanticallyNear(ctx.Query.Value, ctx.Query.Edits(ctx.Query.Value))
                        .ToTerms(ctx.Query.Field);
                }
                else if (ctx.Query.Prefix)
                {
                    terms = reader.StartsWith(ctx.Query.Value)
                        .ToTerms(ctx.Query.Field);
                }
                else
                {
                    terms = reader.IsWord(ctx.Query.Value)
                        .ToTerms(ctx.Query.Field);
                }
            }

            if (Log.IsDebugEnabled && terms.Count > 1)
            {
                Log.DebugFormat("expanded {0}: {1}",
                    ctx.Query.Value, string.Join(" ", terms.Select(t => t.Word.Value)));
            }

            var postings = GetPostings(terms);
            ctx.Scores = Score(postings);
        }

        private void PhraseScan(QueryContext ctx)
        {
            var tokens = ((PhraseQuery)ctx.Query).Values;
            var postingsMatrix = new List<DocumentPosting>[tokens.Count];

            for (int index = 0;index < tokens.Count; index++)
            {
                postingsMatrix[index] = new List<DocumentPosting>();

                var token = tokens[index];
                IList<Term> terms;

                using (var reader = GetTreeReader(ctx.Query.Field))
                {
                    if (ctx.Query.Fuzzy)
                    {
                        terms = reader.SemanticallyNear(token, ctx.Query.Edits(token))
                            .ToTerms(ctx.Query.Field);
                    }
                    else if (ctx.Query.Prefix)
                    {
                        terms = reader.StartsWith(token)
                            .ToTerms(ctx.Query.Field);
                    }
                    else
                    {
                        terms = reader.IsWord(token)
                            .ToTerms(ctx.Query.Field);
                    }
                }

                var postings = terms.Count > 0 ? _readSession.ReadPostings(terms).Sum() : null;
                if (postings != null)
                {
                    postingsMatrix[index].AddRange(postings);
                }
                
            }

            ctx.Scores = ScorePhrase(postingsMatrix);
        }

        private void RangeScan(QueryContext ctx)
        {
            IList<Term> terms;

            using (var reader = GetTreeReader(ctx.Query.Field))
            {
                terms = reader.Range(ctx.Query.Value, ((RangeQuery)ctx.Query).ValueUpperBound)
                        .ToTerms(ctx.Query.Field);
            }

            var postings = GetPostings(terms);
            ctx.Scores = Score(postings);
        }

        private IList<DocumentPosting> GetPostings(IList<Term> terms)
        {
            var postings = terms.Count > 0 ? _readSession.ReadPostings(terms) : null;

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

        private IList<DocumentScore> Score (IList<DocumentPosting> postings)
        {
            var scoreTime = Stopwatch.StartNew();
            var scores = new List<DocumentScore>(postings.Count);

            if (postings != null && postings.Count > 0)
            {
                var docsWithTerm = postings.Count;
                var scorer = _scorerFactory.CreateScorer(_readSession.Version.DocumentCount, docsWithTerm);
                var postingsByDoc = postings.GroupBy(p => p.DocumentId);

                foreach (var posting in postingsByDoc)
                {
                    var docId = posting.Key;
                    var docHash = _readSession.ReadDocHash(docId);

                    if (!docHash.IsObsolete)
                    {
                        var score = scorer.Score(posting.Count());

                        scores.Add(new DocumentScore(docId, docHash.Hash, score, _readSession.Version));
                    }
                }
            }

            Log.DebugFormat("scored in {0}", scoreTime.Elapsed);

            return scores;
        }

        private IList<DocumentScore> ScorePhrase(IList<DocumentPosting>[] postings)
        {
            var scoreTime = Stopwatch.StartNew();
            var weights = new List<DocumentScore>[postings.Length];

            for (int index = 0; index < weights.Length; index++)
            {
                SetWeights(index, postings, weights);
            }

            Log.DebugFormat("scored phrase in {0}", scoreTime.Elapsed);

            return weights.Sum();
        }

        private void SetWeights(int listIndex, IList<DocumentPosting>[] postings, IList<DocumentScore>[] weights)
        {
            var w = new List<DocumentScore>();
            weights[listIndex] = w;

            var maxDistance = postings.Length - 1;
            var first = postings[listIndex];
            var other = postings[postings.Length - (1 + listIndex)];
            var otherIndex = 0;

            for (int i = 0; i < first.Count; i++)
            {
                var firstPosting = first[i];
                var docHash = _readSession.ReadDocHash(firstPosting.DocumentId);
                var score = 0;

                if (docHash.IsObsolete)
                {
                    continue;
                }

                if (otherIndex + 1 <= other.Count)
                {
                    var otherPosting = other[otherIndex];
                    var end = other.Count - 1;
                    while (otherIndex < end)
                    {
                        if (otherPosting.DocumentId == firstPosting.DocumentId) break;

                        otherPosting = other[++otherIndex];
                    }

                    if (otherPosting.DocumentId == firstPosting.DocumentId)
                    {
                        var distance = Math.Abs(firstPosting.Position - otherPosting.Position);

                        if (distance <= maxDistance)
                        {
                            score = 1;
                        }
                    }

                    w.Add(
                        new DocumentScore(
                            firstPosting.DocumentId, docHash.Hash, score, _readSession.Version));
                }
                
            }
        }

        private ITrieReader GetTreeReader(string field)
        {
            var key = field.ToHash();
            long offset;

            if (_readSession.Version.FieldOffsets.TryGetValue(key, out offset))
            {
                _readSession.Stream.Seek(offset, SeekOrigin.Begin);
                return new MappedTrieReader(_readSession.Stream);
            }
            return null;
        }

        public void Dispose()
        {
        }
    }
}