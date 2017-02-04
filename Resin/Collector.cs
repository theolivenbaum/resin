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
using Resin.Sys;

namespace Resin
{
    public class Collector : IDisposable
    {
        private readonly string _directory;
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly IxInfo _ix;
        private readonly IDictionary<Term, IList<DocumentPosting>> _termCache;
        private readonly Dictionary<string, PostingsReader> _postingReaders;
        private readonly IScoringScheme _scorer;
        private readonly IList<LcrsTreeReader> _trieReaders;

        public Collector(string directory, IxInfo ix, IScoringScheme scorer)
        {
            _directory = directory;
            _ix = ix;
            _termCache = new Dictionary<Term, IList<DocumentPosting>>();
            _postingReaders = new Dictionary<string, PostingsReader>();
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

            var time = Time();

            DoScan(query);

            foreach (var child in query.Children)
            {
                DoScan(child);
            }
         
            Log.DebugFormat("scanned {0} in {1}", query, time.Elapsed);
        }

        private void DoScan(QueryContext query)
        {
            if (query.Fuzzy)
            {
                query.Terms = GetTreeReader(query.Field).Near(query.Value, query.Edits)
                    .Select(word => new Term(query.Field, word));
            }
            else if (query.Prefix)
            {
                query.Terms = GetTreeReader(query.Field).StartsWith(query.Value)
                    .Select(word => new Term(query.Field, word));
            }
            else
            {
                if (GetTreeReader(query.Field).HasWord(query.Value))
                {
                    query.Terms = new List<Term> { new Term(query.Field, new Word(query.Value)) };
                }
                else
                {
                    query.Terms = new List<Term>();
                }
            }
        }

        private void GetPostings(QueryContext query)
        {
            if (query == null) throw new ArgumentNullException("query");

            var time = Time();

            DoGetPostings(query);

            foreach (var child in query.Children)
            {
                DoGetPostings(child);
            }

            Log.DebugFormat("got postings for {0} in {1}", query, time.Elapsed);
        }

        private void DoGetPostings(QueryContext query)
        {
            IEnumerable<DocumentPosting> result = null;
            bool first = true;
            foreach (IEnumerable<DocumentPosting> postings in GetPostings(query.Terms))
            {
                if (first)
                {
                    first = false;
                    result = postings;
                    continue;
                }
                result = DocumentPosting.JoinOr(result, postings);
            }
            query.Postings = result ?? Enumerable.Empty<DocumentPosting>();
        }

        private IEnumerable<IEnumerable<DocumentPosting>> GetPostings(IEnumerable<Term> terms)
        {
            foreach (var term in terms)
            {
                IList<DocumentPosting> postings;

                if (!_termCache.TryGetValue(term, out postings))
                {
                    var fileId = term.ToPostingsFileId();
                    var fileName = Path.Combine(_directory, fileId + ".pos");
                    PostingsReader reader;

                    if (!_postingReaders.TryGetValue(fileId, out reader))
                    {
                        if (!_postingReaders.TryGetValue(fileId, out reader))
                        {
                            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                            var sr = new StreamReader(fs, Encoding.ASCII);

                            reader = new PostingsReader(sr);

                            _postingReaders.Add(fileId, reader);
                        }
                    }

                    postings = reader.Read(term).ToList();
                    _termCache.Add(term, postings);
                }
                yield return postings;
            }
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

        private Stopwatch Time()
        {
            var timer = new Stopwatch();
            timer.Start();
            return timer;
        }

        public void Dispose()
        {
            if (_postingReaders != null)
            {
                foreach (var dr in _postingReaders.Values)
                {
                    if(dr != null) dr.Dispose();
                }
            }

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