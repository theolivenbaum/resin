using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using log4net;
using Resin.Analysis;
using Resin.IO.Read;
using Resin.Querying;
using Resin.Sys;

namespace Resin.IO
{
    public class Collector : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly string _directory;
        private readonly IxInfo _ix;
        private readonly IScoringScheme _scorerFactory;
        private readonly int _documentCount;
        private readonly IDistanceResolver _distanceResolver;
        private readonly IDictionary<Query, IList<DocumentScore>> _scoreCache;
        private readonly DocHashReader _docHashReader;
        private readonly PostingsReader _postingsReader;

        public IxInfo Ix { get { return _ix; } }

        public Collector(string directory, IxInfo ix, IScoringScheme scorerFactory = null, int documentCount = -1)
        {
            _directory = directory;
            _ix = ix;
            _scorerFactory = scorerFactory;
            _documentCount = documentCount == -1 ? ix.DocumentCount : documentCount;
            _scoreCache = new Dictionary<Query, IList<DocumentScore>>();

            var docHashesFileName = Path.Combine(_directory, string.Format("{0}.{1}", _ix.VersionId, "pk"));
            var posFileName = Path.Combine(directory, string.Format("{0}.{1}", ix.VersionId, "pos"));

            _docHashReader = new DocHashReader(docHashesFileName);
            _postingsReader = new PostingsReader(new FileStream(posFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096 * 1, FileOptions.SequentialScan));
        }

        public IList<DocumentScore> Collect(QueryContext query)
        {
            var scoreTime = new Stopwatch();
            scoreTime.Start();

            foreach (var subQuery in query.ToList())
            {
                Map(subQuery);
            }

            var reduceTime = new Stopwatch();
            reduceTime.Start();

            var reduced = query.Reduce().ToList();

            Log.DebugFormat("reduced query {0} producing {1} scores in {2}", query, reduced.Count, scoreTime.Elapsed);

            return reduced;
        }

        private void Map(QueryContext subQuery)
        {
            GetTerms(subQuery);
            Score(subQuery);
        }

        private void Scan(IEnumerable<QueryContext> queries)
        {
            //Parallel.ForEach(queries, GetTerms);

            foreach (var q in queries)
            {
                GetTerms(q);
            }
        }

        private void GetTerms(QueryContext query)
        {
            var time = new Stopwatch();
            time.Start();

            var reader = GetTreeReader(query.Field);

            if (reader == null)
            {
                query.Terms = Enumerable.Empty<Term>();
            }
            else
            {
                using (reader)
                {
                    IList<Term> terms;
                    if (query.Fuzzy)
                    {
                        terms = reader.Near(query.Value, query.Edits)
                            .Select(word => new Term(query.Field, word))
                            .ToList();
                    }
                    else if (query.Prefix)
                    {
                        terms = reader.StartsWith(query.Value)
                            .Select(word => new Term(query.Field, word))
                            .ToList();
                    }
                    else if (query.Range)
                    {
                        terms = reader.WithinRange(query.Value, query.ValueUpperBound)
                            .Select(word => new Term(query.Field, word))
                            .ToList();
                    }
                    else
                    {
                        terms = reader.IsWord(query.Value)
                            .Select(word => new Term(query.Field, word))
                            .ToList();
                    }

                    if (Log.IsDebugEnabled && terms.Count > 1)
                    {
                        Log.DebugFormat("expanded {0}: {1}", 
                            query.Value, string.Join(" ", terms.Select(t => t.Word.Value)));
                    }
                    query.Terms = terms;
                }
            }

            Log.DebugFormat("scanned {0} in {1}", query.Serialize(), time.Elapsed);

            GetPostings(query);
        }

        private void GetPostings(QueryContext query)
        {
            var time = new Stopwatch();
            time.Start();

            var terms = query.Terms.ToList();
            var postings = terms.Count > 0 ? ReadPostings(terms).ToList() : null;

            IEnumerable<DocumentPosting> result;

            if (postings == null)
            {
                result = null;
                
            }
            else
            {
                result = postings.Sum();
            }

            query.Postings = result;

            Log.DebugFormat("read postings for {0} in {1}", query.Serialize(), time.Elapsed);
        }
        
        private IEnumerable<IList<DocumentPosting>> ReadPostings(IEnumerable<Term> terms)
        {
            var addresses = terms.Select(term => term.Word.PostingsAddress)
                .OrderBy(adr => adr.Position).ToList();

            var postings = _postingsReader.Read(addresses).SelectMany(x => x).ToList();

            yield return postings;
        }

        private void Score(IEnumerable<QueryContext> queries)
        {
            Parallel.ForEach(queries, Score);
        }

        private void Score(QueryContext query)
        {
            if (query.Postings == null)
            {
                query.Scored = new List<DocumentScore>();
            }
            else
            {
                query.Scored = DoScore(query.Postings.OrderBy(p => p.DocumentId).ToList());
            }
        }

        private IEnumerable<DocumentScore> DoScore(IList<DocumentPosting> postings)
        {
            if (_scorerFactory == null)
            {
                foreach (var posting in postings.OrderBy(p => p.DocumentId))
                {
                    var docHash = _docHashReader.Read(posting.DocumentId);

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

                    foreach (var posting in postings.OrderBy(p => p.DocumentId))
                    {
                        var docHash = _docHashReader.Read(posting.DocumentId);

                        if (!docHash.IsObsolete)
                        {
                            var score = scorer.Score(posting);

                            yield return new DocumentScore(posting.DocumentId, docHash.Hash, score, _ix);
                        }
                    }
                }
            }
        }

        private ITrieReader GetTreeReader(string field)
        {
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}.tri", 
                _ix.VersionId, field.ToHash()));

            if (!File.Exists(fileName)) return null;

            return new MappedTrieReader(fileName);
        }

        public void Dispose()
        {
            _docHashReader.Dispose();
            _postingsReader.Dispose();
        }
    }
}