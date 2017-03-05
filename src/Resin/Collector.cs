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
        private readonly IScoringScheme _scorer;

        public Collector(string directory, IxInfo ix, IScoringScheme scorer)
        {
            _directory = directory;
            _ix = ix;
            _scorer = scorer;
        }

        public IList<DocumentPosting> Collect(QueryContext query)
        {
            Scan(query);
            GetPostings(query);

            var time = Time();
            var trimmed = query.Reduce().Where(d=>_ix.Deletions.Contains(d.DocumentId) == false).ToList();

            Log.DebugFormat("reduced {0} in {1}", query, time.Elapsed);

            return trimmed;
        }

        private void Scan(QueryContext query)
        {
            if (query == null) throw new ArgumentNullException("query");

            Parallel.ForEach(new List<QueryContext> {query}.Concat(query.Children), DoScan);
            //foreach (var q in new List<QueryContext> { query }.Concat(query.Children))
            //{
            //    DoScan(q);
            //}
        }

        private void DoScan(QueryContext query)
        {
            var time = Time();
            var terms = new List<Term>();
            var reader = GetTreeReader(query.Field);

            if (query.Fuzzy)
            {
                terms.AddRange(reader.Near(query.Value, query.Edits).Select(word => new Term(query.Field, word)));
            }
            else if (query.Prefix)
            {
                terms.AddRange(reader.StartsWith(query.Value).Select(word => new Term(query.Field, word)));
            }
            else
            {
                if (reader.HasWord(query.Value))
                {
                    terms.Add(new Term(query.Field, new Word(query.Value)));
                }
            }

            query.Terms = terms;
            Log.DebugFormat("scanned {0} in {1}", query.AsReadable(), time.Elapsed);
        }

        private void GetPostings(QueryContext query)
        {
            if (query == null) throw new ArgumentNullException("query");
            
            Parallel.ForEach(new List<QueryContext> {query}.Concat(query.Children), DoGetPostings);
        }

        private void DoGetPostings(QueryContext query)
        {
            var time = Time();

            var result = DoReadPostings(query.Terms)
                .Aggregate<IEnumerable<DocumentPosting>, IEnumerable<DocumentPosting>>(
                    null, DocumentPosting.JoinOr).ToList();

            query.Postings = result;

            Log.DebugFormat("read postings for {0} in {1}", query.AsReadable(), time.Elapsed);

        }
        
        private IEnumerable<IEnumerable<DocumentPosting>> DoReadPostings(IEnumerable<Term> terms)
        {
            var result = new ConcurrentBag<List<DocumentPosting>>();

            Parallel.ForEach(terms, term =>
            {
                IList<DocumentPosting> postings = GetPostingsReader(term).Read(term).ToList();
                postings = Score(postings).ToList();
                result.Add(new List<DocumentPosting>(postings));
            });

            return result;
        }

        private PostingsReader GetPostingsReader(Term term)
        {
            var fileId = term.ToPostingsFileId();
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}.pos", _ix.Name, fileId));
            var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            var sr = new StreamReader(fs, Encoding.Unicode);

            return new PostingsReader(sr);
        }

        private IEnumerable<DocumentPosting> Score(IList<DocumentPosting> postings)
        {
            if (postings.Any())
            {
                var scorer = _scorer.CreateScorer(_ix.DocumentCount.DocCount[postings.First().Field], postings.Count);

                foreach (var posting in postings)
                {
                    posting.Scoring = new DocumentScore(posting.DocumentId, posting.Count);
                    posting.IndexName = _ix.Name;

                    scorer.Score(posting.Scoring);

                    yield return posting;
                }
            }
        }

        private ITrieReader GetTreeReader(string field)
        {
            var fileId = field.ToTrieFileId();
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}.tri", _ix.Name, fileId));
            var reader = new StreamingTrieReader(fileName);

            return reader;
        }

        private static Stopwatch Time()
        {
            var timer = new Stopwatch();
            timer.Start();
            return timer;
        }

        public void Dispose()
        {
        }
    }
}