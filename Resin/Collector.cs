using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using log4net;
using Newtonsoft.Json;
using Resin.IO;

namespace Resin
{
    public class Collector
    {
        private readonly string _directory;
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly IndexInfo _ix;
        private readonly IDictionary<Term, List<DocumentWeight>> _termCache;
 
        public Collector(string directory, IndexInfo ix)
        {
            _directory = directory;
            _ix = ix;
            _termCache = new Dictionary<Term, List<DocumentWeight>>();
        }

        public IEnumerable<DocumentScore> Collect(QueryContext queryContext, int page, int size, IScoringScheme scorer)
        {
            Expand(queryContext);
            Scan(queryContext, scorer);

            var scored = queryContext.Resolve().Values
                .OrderByDescending(s => s.Score)
                .Skip(page*size)
                .Take(size)
                .ToList();

            return scored;
        }

        private IList<DocumentWeight> GetWeights(Term term)
        {
            List<DocumentWeight> weights;
            if (!_termCache.TryGetValue(term, out weights))
            {
                int rowIndex;
                if (_ix.PostingAddressByTerm.TryGetValue(term, out rowIndex))
                {
                    return ReadWeights(Path.Combine(_directory, "0.pos"), rowIndex);
                }
            }
            return new List<DocumentWeight>();
        }

        private IList<DocumentWeight> ReadWeights(string fileName, int rowIndex)
        {
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fs, Encoding.Unicode))
            {
                var row = 0;
                while (row++ < rowIndex)
                {
                    reader.ReadLine();
                }
                return JsonConvert.DeserializeObject<IList<DocumentWeight>>(reader.ReadLine());
            }
        } 

        private LcrsTreeReader GetReader(string field)
        {
            var timer = new Stopwatch();
            timer.Start();
            var fileName = Path.Combine(_directory, field.ToTrieFileId() + ".tri");
            var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sr = new StreamReader(fs, Encoding.Unicode);
            var reader = new LcrsTreeReader(sr);
            Log.DebugFormat("opened {0} in {1}", fileName, timer.Elapsed);
            return reader;
        }

        private void Scan(QueryContext queryContext, IScoringScheme scorer)
        {
            queryContext.Result = GetScoredResult(queryContext, scorer)
                .GroupBy(s=>s.DocId)
                .Select(g=>new DocumentScore(g.Key, g.Sum(s=>s.Score)))
                .ToDictionary(x => x.DocId, y => y);

            foreach (var child in queryContext.Children)
            {
                Scan(child, scorer);
            }
        }

        private IEnumerable<DocumentScore> GetScoredResult(QueryTerm queryTerm, IScoringScheme scoringScheme)
        {
            using (var reader = GetReader(queryTerm.Field))
            {
                var totalNumOfDocs = _ix.DocumentCount.DocCount[queryTerm.Field];
                if (reader.HasWord(queryTerm.Value))
                {
                    var weights = GetWeights(new Term(queryTerm.Field, queryTerm.Value));
                    var scorer = scoringScheme.CreateScorer(totalNumOfDocs, weights.Count);
                    foreach (var weight in weights)
                    {
                        var hit = new DocumentScore(weight.DocumentId, weight.Weight);
                        scorer.Score(hit);
                        yield return hit;
                    }
                }
            }
        }

        private void Expand(QueryContext queryContext)
        {
            if (queryContext == null) throw new ArgumentNullException("queryContext");
            
            if (queryContext.Fuzzy || queryContext.Prefix)
            {
                var timer = new Stopwatch();
                timer.Start();
                using (var reader = GetReader(queryContext.Field))
                {
                    IList<QueryContext> expanded = null;

                    if (queryContext.Fuzzy)
                    {
                        expanded = reader.Near(queryContext.Value, queryContext.Edits).Select(token => new QueryContext(queryContext.Field, token)).ToList();
                    }
                    else if (queryContext.Prefix)
                    {
                        expanded = reader.StartsWith(queryContext.Value).Select(token => new QueryContext(queryContext.Field, token)).ToList();
                    }

                    if (expanded != null)
                    {
                        foreach (var t in expanded.Where(e => e.Value != queryContext.Value))
                        {
                            queryContext.Children.Add(t);
                        }
                    }

                    queryContext.Prefix = false;
                    queryContext.Fuzzy = false;

                    Log.DebugFormat("expanded {0} in {1}", queryContext, timer.Elapsed); 
                }                
            }

            foreach (var child in queryContext.Children)
            {
                Expand(child);
            }
        }
    }
}