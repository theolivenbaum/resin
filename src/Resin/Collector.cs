using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private readonly int _documentCount;

        public IxInfo Ix { get { return _ix; } }

        public Collector(string directory, IxInfo ix, IScoringScheme scorerFactory = null, int documentCount = -1)
        {
            _directory = directory;
            _ix = ix;
            _scorerFactory = scorerFactory;

            _documentCount = documentCount == -1 ? ix.DocumentCount : documentCount;
        }

        public IList<DocumentScore> Collect(QueryContext query)
        {
            var queries = query.ToList();

            Scan(queries);

            var scoreTime = new Stopwatch();
            scoreTime.Start();

            Score(queries);

            Log.DebugFormat("scored query {0} in {1}", query, scoreTime.Elapsed);

            var reduceTime = new Stopwatch();
            reduceTime.Start();

            var reduced = query.Reduce().ToList();

            Log.DebugFormat("reduced query {0} producing {1} scores in {2}", query, reduced.Count, scoreTime.Elapsed);

            return reduced;
        }

        private void Scan(IEnumerable<QueryContext> queries)
        {
            //Parallel.ForEach(queries, DoScan);

            foreach (var q in queries)
            {
                DoScan(q);
            }
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

            var terms = query.Terms.ToList();

            var postings = terms.Count > 0 ? ReadPostings(terms).ToList() : new List<IList<DocumentPosting>>();

            var result = postings
                    .Aggregate<IEnumerable<DocumentPosting>, IEnumerable<DocumentPosting>>(
                        null, DocumentPosting.Join);

            query.Postings = result;

            Log.DebugFormat("read postings for {0} in {1}", query.AsReadable(), time.Elapsed);
        }
        
        private IEnumerable<IList<DocumentPosting>> ReadPostings(IEnumerable<Term> terms)
        {
            return PostingsReader.ReadPostings(_directory, _ix, terms);
        }

        private void Score(IEnumerable<QueryContext> queries)
        {
            Parallel.ForEach(queries, query =>
            {
                if (query.Postings == null)
                {
                    query.Scored = new List<DocumentScore>();
                }
                else
                {
                    query.Scored = DoScore(query.Postings.OrderBy(p => p.DocumentId).ToList());
                }
            });
        }

        private IEnumerable<DocumentScore> DoScore(IList<DocumentPosting> postings)
        {
            var docHashesFileName = Path.Combine(_directory, string.Format("{0}.{1}", _ix.VersionId, "pk"));

            using (var docHashReader = new DocHashReader(docHashesFileName))
            {
                if (_scorerFactory == null)
                {
                    foreach (var posting in postings)
                    {
                        var docHash = docHashReader.Read(posting.DocumentId);

                        if (!docHash.IsObsolete)
                        {
                            yield return new DocumentScore(posting.DocumentId, docHash.Hash, 0, _ix);
                        }
                    }
                }
                else
                {
                    if (postings.Any())
                    {
                        var docsWithTerm = postings.Count;

                        var scorer = _scorerFactory.CreateScorer(_documentCount, docsWithTerm);

                        foreach (var posting in postings)
                        {
                            var docHash = docHashReader.Read(posting.DocumentId);

                            if (!docHash.IsObsolete)
                            {
                                var score = scorer.Score(posting);

                                yield return new DocumentScore(posting.DocumentId, docHash.Hash, score, _ix);
                            }
                        }
                    }
                }
            }
        }

        private ITrieReader GetTreeReader(string field, string token)
        {
            var suffix = token.ToTokenBasedBucket();
            var fileId = field.ToHash();
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}-{2}.tri", 
                _ix.VersionId, fileId, suffix));

            if (!File.Exists(fileName)) return null;

            var reader = new MappedTrieReader(fileName);

            return reader;
        }

        public void Dispose()
        {
        }
    }
}