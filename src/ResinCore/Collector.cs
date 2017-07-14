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
            _scorerFactory = scorerFactory;
            _scoreCache = new Dictionary<Query, IList<DocumentScore>>();
        }

        public DocumentScore[] Collect(QueryContext query)
        {
            var scoreTime = new Stopwatch();
            scoreTime.Start();

            var queries = query.ToList();
            foreach (var subQuery in queries)
            {
                Scan(subQuery);
                GetPostings(subQuery);
                Score(subQuery);
            }

            Log.DebugFormat("scored query {0} in {1}", query, scoreTime.Elapsed);

            var reduceTime = new Stopwatch();
            reduceTime.Start();

            var reduced = query.Reduce().ToArray();

            Log.DebugFormat("reduced query {0} producing {1} scores in {2}", query, reduced.Length, scoreTime.Elapsed);

            return reduced;
        }

        private void Scan(QueryContext subQuery)
        {
            var time = new Stopwatch();
            time.Start();

            var reader = GetTreeReader(subQuery.Field);

            if (reader == null)
            {
                subQuery.Terms = new List<Term>(0);
            }
            else
            {
                using (reader)
                {
                    IList<Term> terms;

                    if (subQuery.Fuzzy)
                    {
                        terms = reader.SemanticallyNear(subQuery.Value, subQuery.Edits)
                            .ToTerms(subQuery.Field);
                    }
                    else if (subQuery.Prefix)
                    {
                        terms = reader.StartsWith(subQuery.Value)
                            .ToTerms(subQuery.Field);
                    }
                    else if (subQuery.Range)
                    {
                        terms = reader.Range(subQuery.Value, subQuery.ValueUpperBound)
                            .ToTerms(subQuery.Field);
                    }
                    else
                    {
                        terms = reader.IsWord(subQuery.Value)
                            .ToTerms(subQuery.Field);
                    }

                    subQuery.Terms = terms;

                    if (Log.IsDebugEnabled && terms.Count > 1)
                    {
                        Log.DebugFormat("expanded {0}: {1}", 
                            subQuery.Value, string.Join(" ", terms.Select(t => t.Word.Value)));
                    }
                }
            }

            Log.DebugFormat("scanned {0} in {1}", subQuery.Serialize(), time.Elapsed);
        }

        private void GetPostings(QueryContext subQuery)
        {
            var time = Stopwatch.StartNew();

            var postings = subQuery.Terms.Count > 0 ? _readSession.ReadPostings(subQuery.Terms) : null;

            IList<DocumentPosting> reduced;

            if (postings == null)
            {
                reduced = null;
            }
            else
            {
                reduced = postings.Sum();
            }

            subQuery.Postings = reduced;

            Log.DebugFormat("read postings for {0} in {1}", subQuery.Serialize(), time.Elapsed);
        }

        private void Score(QueryContext query)
        {
            if (query.Postings == null)
            {
                query.Scored = new List<DocumentScore>();
            }
            else
            {
                query.Scored = DoScore(query.Postings);
            }
        }

        private IList<DocumentScore> DoScore(IList<DocumentPosting> postings)
        {
            var scores = new List<DocumentScore>(postings.Count);

            if (_scorerFactory == null)
            {
                foreach (var posting in postings)
                {
                    var docHash = _readSession.ReadDocHash(posting.DocumentId);

                    if (!docHash.IsObsolete)
                    {
                        scores.Add(new DocumentScore(posting.DocumentId, docHash.Hash, 0, _readSession.Version));
                    }
                }
            }
            else
            {
                if (postings.Any())
                {
                    var docsWithTerm = postings.Count;

                    var scorer = _scorerFactory.CreateScorer(_readSession.Version.DocumentCount, docsWithTerm);

                    foreach (var posting in postings)
                    {
                        var docHash = _readSession.ReadDocHash(posting.DocumentId);

                        if (!docHash.IsObsolete)
                        {
                            var score = scorer.Score(posting);

                            scores.Add(new DocumentScore(posting.DocumentId, docHash.Hash, score, _readSession.Version));
                        }
                    }
                }
            }
            return scores;
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