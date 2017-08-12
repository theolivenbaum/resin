using System;
using System.Collections.Generic;
using System.Diagnostics;
using log4net;
using Resin.Analysis;
using Resin.Querying;
using Resin.Sys;
using StreamIndex;
using DocumentTable;

namespace Resin
{
    /// <summary>
    /// Query a index.
    /// </summary>
    public class Searcher : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));
        private readonly string _directory;
        private readonly QueryParser _parser;
        private readonly IScoringSchemeFactory _scorerFactory;
        private readonly IList<SegmentInfo> _versions;
        private readonly int _blockSize;
        private readonly IReadSessionFactory _sessionFactory;

        public Searcher(string directory)
            :this(directory, new QueryParser(new Analyzer()), new TfIdfFactory(), new FullTextReadSessionFactory(directory))
        {
        }

        public Searcher(string directory, long version)
            : this(directory, version, new QueryParser(new Analyzer()), new TfIdfFactory(), new FullTextReadSessionFactory(directory))
        {
        }

        public Searcher(string directory, QueryParser parser, IScoringSchemeFactory scorerFactory, IReadSessionFactory sessionFactory = null)
        {
            _directory = directory;
            _parser = parser;
            _scorerFactory = scorerFactory;
            _versions = Util.GetIndexVersionListInChronologicalOrder(directory);
            _blockSize = BlockSerializer.SizeOfBlock();
            _sessionFactory = sessionFactory ?? new FullTextReadSessionFactory(directory);
        }

        public Searcher(string directory, long version, QueryParser parser, IScoringSchemeFactory scorerFactory, IReadSessionFactory sessionFactory = null)
        {
            _directory = directory;
            _parser = parser;
            _scorerFactory = scorerFactory;
            _versions = new List<SegmentInfo> { { SegmentInfo.Load(directory, version) } };
            _blockSize = BlockSerializer.SizeOfBlock();
            _sessionFactory = sessionFactory ?? new FullTextReadSessionFactory(directory);
        }

        public ScoredResult Search(string query, int page = 0, int size = 10)
        {
            var queryContext = _parser.Parse(query);

            return Search(queryContext, page, size);
        }

        public ScoredResult Search(IList<QueryContext> query, int page = 0, int size = 10)
        {
            if (query == null)
            {
                return new ScoredResult { Docs = new List<ScoredDocument>() };
            }

            var searchTime = Stopwatch.StartNew();
            var scores = Collect(query);
            var docTime = Stopwatch.StartNew();
            int total;
            var docs = GetDocs(scores, page * size, size, out total);

            Log.DebugFormat("fetched {0} docs for query {1} in {2}", docs.Count, query.ToQueryString(), docTime.Elapsed);

            var result = new ScoredResult
            {
                Total = total,
                Docs = docs
            };

            Log.DebugFormat("total search time for query {0} in {1}", query.ToQueryString(), searchTime.Elapsed);

            return result;
        }

        private IList<List<DocumentScore>> Collect(IList<QueryContext> query)
        {
            var scores = new List<DocumentScore>[_versions.Count];

            for (var index = 0;index<_versions.Count;index++)
            {
                using (var readSession = _sessionFactory.OpenReadSession(_versions[index]))
                {
                    scores[index] = Collect(query, readSession);
                }
            }

            return scores;
        }

        private List<DocumentScore> Collect(IList<QueryContext> query, IReadSession readSession)
        {
            using (var collector = new Collector(_directory, readSession, _scorerFactory))
            {
                return collector.Collect(query);
            }
        }

        private IList<ScoredDocument> GetDocs(
            IList<List<DocumentScore>> scores, int skip, int size, out int total)
        {
            var paged = scores.SortByScoreAndTakeLatestVersion(skip, size, out total);
            var result = new List<ScoredDocument>();

            foreach(var score in paged)
            {
                result.Add(GetDoc(score));
            }

            return result;
        }

        private ScoredDocument GetDoc(DocumentScore score)
        {
            using (var readSession = (FullTextReadSession)_sessionFactory.OpenReadSession(score.Ix))
            {
                return readSession.ReadDocuments(score);
            }
        }

        public void Dispose()
        {
            ((IDisposable)_sessionFactory).Dispose();
        }
    }
}