using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly string _directory;
        private readonly IxInfo _ix;
        private readonly IScoringScheme _scorerFactory;
        private readonly IDictionary<string, int> _documentCount;

        public Collector(string directory, IxInfo ix, IScoringScheme scorerFactory = null, IDictionary<string, int> documentCount = null)
        {
            _directory = directory;
            _ix = ix;
            _scorerFactory = scorerFactory;

            _documentCount = documentCount ?? ix.DocumentCount;
        }

        public IList<DocumentScore> Collect(QueryContext query)
        {
            var time = new Stopwatch();
            time.Start();

            var queries = query.ToList();

            Scan(queries);
            Score(queries);

            var reduced = query.Reduce().ToList();
            var result = reduced;

            Log.DebugFormat("collected {0} in {1}", query, time.Elapsed);

            return result;
        }

        private void Scan(IEnumerable<QueryContext> queries)
        {
            Parallel.ForEach(queries, DoScan);

            //foreach (var q in queries)
            //{
            //    DoScan(q);
            //}
        }

        private void DoScan(QueryContext query)
        {
            var time = new Stopwatch();
            time.Start();

            var reader = GetTreeReader(query.Field, query.Value);

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
                    var terms = new List<Term>();
                    Word word;
                    if (reader.HasWord(query.Value, out word))
                    {
                        terms.Add(new Term(query.Field, word));
                    }
                    query.Terms = terms;
                }
            }

            Log.DebugFormat("scanned {0} in {1}", query.AsReadable(), time.Elapsed);

            GetPostings(query);
        }

        private void GetPostings(QueryContext query)
        {
            var time = new Stopwatch();
            time.Start();

            var postings = ReadPostings(query.Terms).ToList();

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
        
        private IEnumerable<IList<DocumentPosting>> ReadPostings(IEnumerable<Term> terms)
        {
            var posFileName = Path.Combine(_directory, string.Format("{0}.{1}", _ix.VersionId, "pos"));

            using (var reader = new PostingsReader(new FileStream(posFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan)))
            {
                var addresses = terms.Select(term => term.Word.PostingsAddress.Value).OrderBy(adr => adr.Position).ToList();

                yield return reader.Get(addresses).SelectMany(x => x).ToList();
            }
        }

        private void Score(IEnumerable<QueryContext> queries)
        {
            Parallel.ForEach(queries, query =>
            {
                query.Scored = DoScore(query.Postings.ToList(), query.Field);
            });
        }

        private IEnumerable<DocumentScore> DoScore(IList<DocumentPosting> postings, string field)
        {
            if (_scorerFactory == null)
            {
                foreach (var posting in postings)
                {
                    yield return new DocumentScore(posting.DocumentId, 0, _ix);
                }
            }
            else
            {
                if (postings.Any())
                {
                    var docsInCorpus = _documentCount[field];
                    var docsWithTerm = postings.Count;

                    var scorer = _scorerFactory.CreateScorer(docsInCorpus, docsWithTerm);

                    foreach (var posting in postings)
                    {
                        var score = scorer.Score(posting);

                        yield return  new DocumentScore(posting.DocumentId, score, _ix);
                    }
                } 
            }
        }

        private ITrieReader GetTreeReader(string field, string token)
        {
            var suffix = token.ToTrieBucketName();
            var fileId = field.ToHashString();
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}-{2}.tri", _ix.VersionId, fileId, suffix));

            if (!File.Exists(fileName)) return null;

            var reader = new MappedTrieReader(fileName);

            return reader;
        }

        public void Dispose()
        {
        }
    }
}