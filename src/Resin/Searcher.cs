using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSharpTest.Net.Collections;
using CSharpTest.Net.Serialization;
using log4net;
using Resin.Analysis;
using Resin.IO;
using Resin.IO.Read;
using Resin.Querying;

namespace Resin
{
    public class Searcher : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));
        private readonly string _directory;
        private readonly QueryParser _parser;
        private readonly IScoringScheme _scorer;
        private readonly IDictionary<string, IxInfo> _indices;
        private readonly DbDocumentReader _docReader;

        public Searcher(string directory, QueryParser parser, IScoringScheme scorer)
        {
            _directory = directory;
            _parser = parser;
            _scorer = scorer;

            var initTimer = Time();
            var ixFiles = GetIndexFileNamesInChronologicalOrder();
            
            _indices = ixFiles.Select(IxInfo.Load).ToDictionary(x => x.Name);
            
            _docReader = new DbDocumentReader(OpenDocDb());
            
            Log.DebugFormat("init searcher in {0}", initTimer.Elapsed);
        }

        private BPlusTree<int, byte[]> OpenDocDb()
        {
            var dbOptions = new BPlusTree<int, byte[]>.OptionsV2(
                PrimitiveSerializer.Int32,
                PrimitiveSerializer.Bytes);

            dbOptions.FileName = Path.Combine(_directory, string.Format("{0}-{1}.{2}", _indices.Values.First().Name, "doc", "db"));
            dbOptions.ReadOnly = true;
            return new BPlusTree<int, byte[]>(dbOptions);
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
            var searchTime = Time();
            var parseTime = Time();

            var queryContext = _parser.Parse(query);

            Log.DebugFormat("parsed query {0} in {1}", queryContext, parseTime.Elapsed);

            if (queryContext == null)
            {
                return new Result { Docs = new List<Document>() };
            }

            var scored = Collect(queryContext);
            var skip = page * size;
            var paged = scored.Skip(skip).Take(size);

            var docTime = Time();

            var docs = paged.Select(GetDoc).ToList();

            Log.DebugFormat("fetched {0} docs for query {1} in {2}", docs.Count, queryContext, docTime.Elapsed);

            var result = new Result { Docs = docs, Total = scored.Count };

            Log.DebugFormat("searched {0} in {1}", queryContext, searchTime.Elapsed);

            return result;
        }

        private string[] GetIndexFileNamesInChronologicalOrder()
        {
            return Directory.GetFiles(_directory, "*.ix").OrderBy(s => s).ToArray();
        }

        private IList<DocumentScore> Collect(QueryContext query)
        {
            var collectors = _indices.Values.Select(ix => new Collector(_directory, ix, _scorer)).ToList();          
            var scores = new ConcurrentBag<IEnumerable<DocumentScore>>();

            Parallel.ForEach(collectors, c => scores.Add(c.Collect(query.Clone())));

            return scores
                .Aggregate(DocumentScore.CombineOr)
                .OrderByDescending(p => p.Score).ToList();
        }

        private Document GetDoc(DocumentScore score)
        {
            var doc = _docReader.Get(score.DocumentId);

            doc.Fields["__score"] = score.Score.ToString(CultureInfo.InvariantCulture);

            return doc; 
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