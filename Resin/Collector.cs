using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Read;
using Resin.Querying;
using Resin.System;

namespace Resin
{
    public class Collector : IDisposable
    {
        private readonly string _directory;
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly IxInfo _ix;
        private readonly IDictionary<Term, IList<DocumentPosting>> _termCache;
        private readonly Dictionary<string, PostingsReader> _readers;

        public Collector(string directory, IxInfo ix)
        {
            _directory = directory;
            _ix = ix;
            _termCache = new Dictionary<Term, IList<DocumentPosting>>();
            _readers = new Dictionary<string, PostingsReader>();
        }

        public IEnumerable<DocumentScore> Collect(QueryContext queryContext, IScoringScheme scorer)
        {
            Expand(queryContext);
            Score(queryContext, scorer);

            var scored = queryContext.Resolve().Values
                .OrderByDescending(s => s.Score)
                .ToList();

            return scored;
        }

        private void Score(QueryContext queryContext, IScoringScheme scorer)
        {
            queryContext.Result = GetScoredResult(queryContext.ToQueryTerm(), scorer)
                .GroupBy(s => s.DocId)
                .Select(g => new DocumentScore(g.Key, g.Sum(s => s.Score)))
                .ToDictionary(x => x.DocId, y => y);

            foreach (var child in queryContext.Children)
            {
                Score(child, scorer);
            }
        }

        private IEnumerable<DocumentScore> GetScoredResult(QueryTerm queryTerm, IScoringScheme scoringScheme)
        {
            var timer = new Stopwatch();
            timer.Start();

            var totalNumOfDocs = _ix.DocumentCount.DocCount[queryTerm.Field];

            var postings = GetPostings(new Term(queryTerm.Field, queryTerm.Value));
            var scorer = scoringScheme.CreateScorer(totalNumOfDocs, postings.Count);

            foreach (var posting in postings)
            {
                var hit = new DocumentScore(posting.DocumentId, posting.Count);

                scorer.Score(hit);
                yield return hit;
            }

            Log.DebugFormat("scored term {0} in {1}", queryTerm, timer.Elapsed);
        }
        
        private IList<DocumentPosting> GetPostings(Term term)
        {
            IList<DocumentPosting> postings;

            if (!_termCache.TryGetValue(term, out postings))
            {
                var timer = new Stopwatch();
                timer.Start();

                var fileId = term.ToPostingsFileId();
                var fileName = Path.Combine(_directory, fileId + ".pos");
                PostingsReader reader;

                if (!_readers.TryGetValue(fileId, out reader))
                {
                    if (!_readers.TryGetValue(fileId, out reader))
                    {
                        var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var sr = new StreamReader(fs, Encoding.ASCII);

                        reader = new PostingsReader(sr);
                        _readers.Add(fileId, reader);
                    }
                }

                postings = reader.Read(term).ToList();
                _termCache.Add(term, postings);

                Log.DebugFormat("read {0} postings from {1} in {2}", postings.Count(), fileName, timer.Elapsed);
            }

            return postings;
        }

        private LcrsTreeReader GetTreeReader(string field)
        {
            var fileName = Path.Combine(_directory, field.ToTrieFileId() + ".tri");
            var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sr = new StreamReader(fs, Encoding.Unicode);
            var reader = new LcrsTreeReader(sr);

            return reader;
        }

        private void Expand(QueryContext queryContext)
        {
            if (queryContext == null) throw new ArgumentNullException("queryContext");

            var timer = new Stopwatch();
            timer.Start();

            using (var reader = GetTreeReader(queryContext.Field))
            {
                IEnumerable<QueryContext> expanded;

                if (queryContext.Fuzzy)
                {
                    expanded = reader.Near(queryContext.Value, queryContext.Edits)
                        .Select(token => new QueryContext(queryContext.Field, token));
                }
                else if (queryContext.Prefix)
                {
                    expanded = reader.StartsWith(queryContext.Value)
                        .Select(token => new QueryContext(queryContext.Field, token));
                }
                else
                {
                    if (reader.HasWord(queryContext.Value))
                    {
                        expanded = new List<QueryContext> {new QueryContext(queryContext.Field, queryContext.Value)};
                    }
                    else
                    {
                        expanded = new List<QueryContext>();
                    }
                }

                queryContext.Prefix = false;
                queryContext.Fuzzy = false;

                foreach (var t in expanded.Where(e => e.Value != queryContext.Value))
                {
                    queryContext.Children.Add(t);
                }

                Log.DebugFormat("expanded {0} into {1} in {2}", queryContext.ToQueryTerm(), queryContext, timer.Elapsed);
            }
        }

        public void Dispose()
        {
            foreach (var dr in _readers.Values)
            {
                dr.Dispose();
            }
        }
    }
}