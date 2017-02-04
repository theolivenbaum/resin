using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Read;
using Resin.Querying;
using Resin.Sys;

namespace Resin
{
    public class Collector : IDisposable
    {
        private readonly string _directory;
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly IxInfo _ix;
        private readonly IDictionary<Term, IList<DocumentPosting>> _termCache;
        private readonly IScoringScheme _scorer;
        private readonly IList<LcrsTreeReader> _trieReaders;

        public Collector(string directory, IxInfo ix, IScoringScheme scorer)
        {
            _directory = directory;
            _ix = ix;
            _termCache = new Dictionary<Term, IList<DocumentPosting>>();
            _scorer = scorer;
            _trieReaders = new List<LcrsTreeReader>();
        }

        public IEnumerable<DocumentScore> Collect(QueryContext query)
        {
            var time = Time();

            Scan(query);
            GetPostings(query);
            Reduce(query);
            Score(query);

            Log.DebugFormat("collected {0} in {1}", query, time.Elapsed);

            return query.Scores.OrderByDescending(s => s.Score);
        }

        private void Scan(QueryContext query)
        {
            if (query == null) throw new ArgumentNullException("query");

            Parallel.ForEach(new List<QueryContext> {query}.Concat(query.Children), DoScan);
                 }

        private void DoScan(QueryContext query)
        {
            var time = Time();

            var reader = GetTreeReader(query.Field);

            if (query.Fuzzy)
            {
                query.Terms = reader.Near(query.Value, query.Edits).Select(word => new Term(query.Field, word));
            }
            else if (query.Prefix)
            {
                query.Terms = reader.StartsWith(query.Value).Select(word => new Term(query.Field, word));
            }
            else
            {
                if (reader.HasWord(query.Value))
                {
                    query.Terms = new List<Term> { new Term(query.Field, new Word(query.Value)) };
                }
                else
                {
                    query.Terms = new List<Term>();
                }
            }

            Log.DebugFormat("scanned {0} in {1}", query, time.Elapsed);
        }

        private void GetPostings(QueryContext query)
        {
            if (query == null) throw new ArgumentNullException("query");

            foreach (var q in new List<QueryContext> {query}.Concat(query.Children))
            {
                DoGetPostings(q);
            }
        }

        private void DoGetPostings(QueryContext query)
        {
            var result = DoReadPostings(query.Terms)
                .Aggregate<IEnumerable<DocumentPosting>, IEnumerable<DocumentPosting>>(null, DocumentPosting.JoinOr);

            query.Postings = result ?? Enumerable.Empty<DocumentPosting>();
        }

        private IEnumerable<IEnumerable<DocumentPosting>> DoReadPostings(IEnumerable<Term> terms)
        {
            var result = new ConcurrentBag<IEnumerable<DocumentPosting>>();

            Parallel.ForEach(terms, term =>
            {
                var time = Time();

                var fileId = term.ToPostingsFileId();
                var fileName = Path.Combine(_directory, fileId + ".pos");
                IList<DocumentPosting> postings;

                if (!_termCache.TryGetValue(term, out postings))
                {
                    postings = GetPostingsReader(term).Read(term).ToList();
                    _termCache.Add(term, postings);
                }

                result.Add(postings);

                Log.DebugFormat("read {0} {1} postings from {2} in {3}", postings.Count, term, fileName, time.Elapsed);
            });

            return result;
        }

        private PostingsReader GetPostingsReader(Term term)
        {
            var fileId = term.ToPostingsFileId();
            var fileName = Path.Combine(_directory, fileId + ".pos");
            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            var sr = new StreamReader(fs, Encoding.ASCII);

            return new PostingsReader(sr);
        }

        private void Reduce(QueryContext query)
        {
            var time = Time();

            DoReduce(query);

            Log.DebugFormat("reduced {0} in {1}", query, time.Elapsed);
        }

        private void DoReduce(QueryContext query)
        {
            query.Reduced = query.Reduce();
        }

        private void Score(QueryContext query)
        {
            var time = Time();

            DoScore(query);

            Log.DebugFormat("scored {0} in {1}", query, time.Elapsed);
        }

        private void DoScore(QueryContext query)
        {
            query.Scores = Score(query.Reduced);
        }

        private IEnumerable<DocumentScore> Score(IEnumerable<DocumentPosting> postings)
        {
            foreach (var posting in postings)
            {
                var scorer = _scorer.CreateScorer(_ix.DocumentCount.DocCount[posting.Term.Field], posting.Count);

                var hit = new DocumentScore(posting.DocumentId, posting.Count);

                scorer.Score(hit);

                yield return hit;
            }
        }
        
        private LcrsTreeReader GetTreeReader(string field)
        {
            var time = Time();
            var fileName = Path.Combine(_directory, field.ToTrieFileId() + ".tri");
            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            var sr = new StreamReader(fs, Encoding.Unicode);
            var reader = new LcrsTreeReader(sr);

            _trieReaders.Add(reader);

            Log.DebugFormat("opened tree reader {0} (field:{1}) in {2}", fileName, field, time.Elapsed);
            
            return reader;
        }

        private Stopwatch Time(bool started = true)
        {
            var timer = new Stopwatch();
            if (started) timer.Start();
            return timer;
        }

        public void Dispose()
        {
            if (_trieReaders != null)
            {
                foreach (var r in _trieReaders)
                {
                    if(r != null) r.Dispose();
                }
            }
        }
    }
}