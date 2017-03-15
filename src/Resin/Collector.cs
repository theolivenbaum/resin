using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSharpTest.Net.Collections;
using CSharpTest.Net.Synchronization;
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
        private readonly BPlusTree<Term, DocumentPosting[]> _postingDb; 

        public Collector(string directory, IxInfo ix, IScoringScheme scorer)
        {
            _directory = directory;
            _ix = ix;
            _scorer = scorer;

            var dbOptions = new BPlusTree<Term, DocumentPosting[]>.OptionsV2(
                new TermSerializer(),
                new ArraySerializer<DocumentPosting>(new PostingSerializer()), new TermComparer());

            dbOptions.FileName = Path.Combine(directory, string.Format("{0}-{1}.{2}", _ix.Name, "pos", "db"));
            dbOptions.ReadOnly = true;
            dbOptions.LockingFactory = new IgnoreLockFactory();

            _postingDb = new BPlusTree<Term, DocumentPosting[]>(dbOptions);
        }

        public IList<DocumentPosting> Collect(QueryContext query)
        {
            Scan(query);

            var time = Time();
            var reduced = query.Reduce().Where(d=>_ix.Deletions.Contains(d.DocumentId) == false).ToList();

            Log.DebugFormat("reduced {0} in {1}", query, time.Elapsed);

            return reduced;
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
            var reader = GetTreeReader(query.Field, query.Value[0]);

            if (reader == null)
            {
                query.Terms = Enumerable.Empty<Term>();
            }
            else
            {
                if (query.Fuzzy)
                {
                    query.Terms = reader.Near(query.Value, query.Edits).Select(word => new Term(query.Field, word)).ToList();
                }
                else if (query.Prefix)
                {
                    query.Terms = reader.StartsWith(query.Value).Select(word => new Term(query.Field, word)).ToList();
                }
                else
                {
                    query.Terms = new[] { new Term(query.Field, new Word(query.Value)) }.ToList();
                }
            }

            Log.DebugFormat("scanned {0} in {1}", query.AsReadable(), time.Elapsed);

            DoGetPostings(query);
        }

        private void DoGetPostings(QueryContext query)
        {
            var time = Time();

            var postings = DoReadPostings(query.Terms).ToList();

            if (postings.Count > 0)
            {
                var result = postings
                    .Aggregate<IEnumerable<DocumentPosting>, IEnumerable<DocumentPosting>>(
                        null, DocumentPosting.Join);

                query.Postings = result;
            }
            else
            {
                query.Postings = new DocumentPosting[0];
            }

            Log.DebugFormat("read postings for {0} in {1}", query.AsReadable(), time.Elapsed);

        }
        
        private IEnumerable<IEnumerable<DocumentPosting>> DoReadPostings(IEnumerable<Term> terms)
        {
            var result = new ConcurrentBag<List<DocumentPosting>>();
            
            foreach(var term in terms)
            {
                var postings = GetPostings(term).ToList();
                postings = Score(postings).ToList();
                result.Add(new List<DocumentPosting>(postings));
            }

            return result;
        }

        private IEnumerable<DocumentPosting> GetPostings(Term term)
        {
            DocumentPosting[] postings;
            if (_postingDb.TryGetValue(term, out postings))
            {
                foreach (var posting in postings)
                {
                    posting.Field = term.Field;
                    yield return posting;
                }
            }
            
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

        private ITrieReader GetTreeReader(string field, char c)
        {
            var suffix = c.ToBucketName();
            var fileId = field.ToTrieFileId();
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}-{2}.tri", _ix.Name, fileId, suffix));

            if (!File.Exists(fileName)) return null;

            var reader = new MappedTrieReader(fileName);

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
            _postingDb.Dispose();
        }
    }
}