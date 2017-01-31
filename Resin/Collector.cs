using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using Resin.IO;
using Resin.IO.Read;

namespace Resin
{
    public class Collector
    {
        private readonly string _directory;
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly IndexInfo _ix;
        private readonly IDictionary<Term, IList<DocumentPosting>> _termCache;
        private PostingsReader _pr;

        public Collector(string directory, IndexInfo ix)
        {
            _directory = directory;
            _ix = ix;
            _termCache = new Dictionary<Term, IList<DocumentPosting>>();
        }

        public IEnumerable<DocumentScore> Collect(QueryContext queryContext, IScoringScheme scorer)
        {
            ScanTermTree(queryContext);
            Scan(queryContext, scorer);

            var scored = queryContext.Resolve().Values
                .OrderByDescending(s => s.Score)
                .ToList();

            return scored;
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
                int rowIndex;
                if (_ix.PostingsAddressByTerm.TryGetValue(term, out rowIndex))
                {
                    postings = ReadPostingsFile(Path.Combine(_directory, "0.pos"), rowIndex);
                    _termCache.Add(term, postings);
                }
            }
            return postings ?? new List<DocumentPosting>();
        }

        private IList<DocumentPosting> ReadPostingsFile(string fileName, int rowIndex)
        {
            var timer = new Stopwatch();
            timer.Start();

            if (_pr == null)
            {
                var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                var sr = new StreamReader(fs, Encoding.Unicode);
                _pr = new PostingsReader(sr);
            }
            var postings = _pr.Read(rowIndex);

            Log.DebugFormat("read row {0} of {1} in {2}", rowIndex, fileName, timer.Elapsed);

            return postings;
        } 

        private LcrsTreeReader GetTreeReader(string field)
        {
            var timer = new Stopwatch();
            timer.Start();

            var fileName = Path.Combine(_directory, field.ToTrieFileId() + ".tri");
            var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sr = new StreamReader(fs, Encoding.Unicode);
            var reader = new LcrsTreeReader(sr);

            return reader;
        }

        private void Scan(QueryContext queryContext, IScoringScheme scorer)
        {
            queryContext.Result = GetScoredResult(queryContext.ToQueryTerm(), scorer)
                .GroupBy(s=>s.DocId)
                .Select(g=>new DocumentScore(g.Key, g.Sum(s=>s.Score)))
                .ToDictionary(x => x.DocId, y => y);

            foreach (var child in queryContext.Children)
            {
                Scan(child, scorer);
            }
        }

        private void ScanTermTree(QueryContext queryContext)
        {
            if (queryContext == null) throw new ArgumentNullException("queryContext");

            var timer = new Stopwatch();
            timer.Start();

            using (var reader = GetTreeReader(queryContext.Field))
            {
                var expanded = new List<QueryContext>();

                if (queryContext.Fuzzy)
                {
                    expanded = reader.Near(queryContext.Value, queryContext.Edits)
                        .Select(token => new QueryContext(queryContext.Field, token))
                        .ToList();
                }
                else if (queryContext.Prefix)
                {
                    expanded = reader.StartsWith(queryContext.Value)
                        .Select(token => new QueryContext(queryContext.Field, token))
                        .ToList();
                }
                else
                {
                    if (reader.HasWord(queryContext.Value))
                    {
                        expanded = new List<QueryContext> { new QueryContext(queryContext.Field, queryContext.Value) };
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
    }
}