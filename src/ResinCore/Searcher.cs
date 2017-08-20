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
        private readonly IQueryParser _parser;
        private readonly IScoringSchemeFactory _scorerFactory;
        private readonly IList<SegmentInfo> _versions;
        private readonly int _blockSize;
        private readonly IReadSessionFactory _sessionFactory;

        public Searcher(string directory)
            :this(directory, new QueryParser(new Analyzer()), new TfIdfFactory(), new FullTextReadSessionFactory(directory))
        {
        }

        public Searcher(string directory, IQueryParser parser = null, IScoringSchemeFactory scorerFactory = null, IReadSessionFactory sessionFactory = null)
        {
            _parser = parser ?? new QueryParser();
            _scorerFactory = scorerFactory ?? new TfIdfFactory();
            _versions = Util.GetIndexVersionListInChronologicalOrder(directory);
            _blockSize = BlockSerializer.SizeOfBlock();
            _sessionFactory = sessionFactory ?? new FullTextReadSessionFactory(directory);
        }

        public Searcher(IReadSessionFactory sessionFactory)
        {
            _parser = new QueryParser();
            _scorerFactory = new TfIdfFactory();
            _versions = Util.GetIndexVersionListInChronologicalOrder(sessionFactory.DirectoryName);
            _blockSize = BlockSerializer.SizeOfBlock();
            _sessionFactory = sessionFactory;
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

            Log.InfoFormat("fetched {0} docs for query {1} in {2}", docs.Count, query.ToQueryString(), docTime.Elapsed);

            var result = new ScoredResult
            {
                Total = total,
                Docs = docs
            };

            Log.DebugFormat("total search time for query {0} in {1}", query.ToQueryString(), searchTime.Elapsed);

            return result;
        }

        private IList<IList<DocumentScore>> Collect(IList<QueryContext> query)
        {
            var scores = new List<IList<DocumentScore>>(_versions.Count);

            for (var index = 0;index<_versions.Count;index++)
            {
                using (var readSession = (IFullTextReadSession)_sessionFactory.OpenReadSession(_versions[index]))
                {
                    scores.Add(Collect(query, readSession));
                }
            }

            return scores;
        }

        private IList<DocumentScore> Collect(IList<QueryContext> query, IFullTextReadSession readSession)
        {
            using (var collector = new Collector(readSession, _scorerFactory))
            {
                return collector.Collect(query);
            }
        }

        private IList<ScoredDocument> GetDocs(
            IList<IList<DocumentScore>> scores, int skip, int size, out int total)
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
            using (var readSession = (IFullTextReadSession)_sessionFactory.OpenReadSession(score.Ix))
            {
                return readSession.ReadDocument(score);
            }
        }

        public void Dispose()
        {
            ((IDisposable)_sessionFactory).Dispose();
        }
    }
}