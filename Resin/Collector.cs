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
        private readonly IScoringScheme _scorer;

        public Collector(string directory, IxInfo ix, IScoringScheme scorer)
        {
            _directory = directory;
            _ix = ix;
            _termCache = new Dictionary<Term, IList<DocumentPosting>>();
            _readers = new Dictionary<string, PostingsReader>();
            _scorer = scorer;
        }

        public IEnumerable<DocumentScore> Collect(QueryContext query)
        {
            Scan(query);
            Map(query);

            return query.Reduce()
                .OrderByDescending(s => s.Score);
        }

        private void Scan(QueryContext query)
        {
            if (query == null) throw new ArgumentNullException("query");

            var timer = Time();

            using (var reader = GetTreeReader(query.Field))
            {
                IEnumerable<QueryContext> found;

                if (query.Fuzzy)
                {
                    found = reader.Near(query.Value, query.Edits)
                        .Select(token => new QueryContext(query.Field, token));
                }
                else if (query.Prefix)
                {
                    found = reader.StartsWith(query.Value)
                        .Select(token => new QueryContext(query.Field, token));
                }
                else
                {
                    if (reader.HasWord(query.Value))
                    {
                        found = new List<QueryContext>();
                    }
                    else
                    {
                        found = null;
                    }
                }

                if (found != null)
                {
                    foreach (var t in found)
                    {
                        query.Children.Add(t);
                    }
                }
            }

            Log.DebugFormat("scanned {0} in {1}", query, timer.Elapsed);
        }

        private void Map(QueryContext query)
        {
            var docsInCorpus = _ix.DocumentCount.DocCount[query.Field];
            var postings = GetPostings(new Term(query.Field, query.Value));
            var scored = Score(postings, docsInCorpus);

            query.Result = scored;

            foreach (var child in query.Children)
            {
                Map(child);
            }
        }

        private IEnumerable<DocumentScore> Score(IList<DocumentPosting> postings, int docsInCorpus)
        {
            var scorer = _scorer.CreateScorer(docsInCorpus, postings.Count);

            foreach (var posting in postings)
            {
                var hit = new DocumentScore(posting.DocumentId, posting.Count);

                scorer.Score(hit);
                yield return hit;
            }
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
            
            return new LcrsTreeReader(sr);
        }

        private Stopwatch Time()
        {
            var timer = new Stopwatch();
            timer.Start();
            return timer;
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