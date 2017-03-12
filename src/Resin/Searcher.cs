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
using CSharpTest.Net.Synchronization;
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

            var ixFiles = GetIndexFileNamesInChronologicalOrder();
            _indices = ixFiles.Select(IxInfo.Load).ToDictionary(x => x.Name);

            _docReader = new DbDocumentReader(OpenDocDb());
        }

        private BPlusTree<int, byte[]> OpenDocDb()
        {
            var dbOptions = new BPlusTree<int, byte[]>.OptionsV2(
                PrimitiveSerializer.Int32,
                PrimitiveSerializer.Bytes);

            dbOptions.FileName = Path.Combine(_directory, string.Format("{0}-{1}.{2}", _indices.Values.First().Name, "doc", "db"));
            dbOptions.ReadOnly = true;
            dbOptions.LockingFactory = new IgnoreLockFactory();

            return new BPlusTree<int, byte[]>(dbOptions);
        }

        public Result Search(string query, int page = 0, int size = 10000, bool returnTrace = false)
        {
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
            var time = Time();
            var docs = paged.Select(GetDoc).ToList();

            Log.DebugFormat("read docs for {0} in {1}", queryContext, time.Elapsed);

            return new Result { Docs = docs, Total = scored.Count }; 
        }

        private string[] GetIndexFileNamesInChronologicalOrder()
        {
            return Directory.GetFiles(_directory, "*.ix").OrderBy(s => s).ToArray();
        }

        private IList<DocumentPosting> Collect(QueryContext query)
        {
            var collectors = _indices.Values.Select(ix => new Collector(_directory, ix, _scorer)).ToList();          
            var postings = new ConcurrentBag<IEnumerable<DocumentPosting>>();

            Parallel.ForEach(collectors, c => postings.Add(c.Collect(query.Clone())));

            return postings
                .Aggregate(DocumentPosting.JoinOrUnbiased)
                .OrderByDescending(p => p.Scoring.Score).ToList();
        }

        private Document GetDoc(DocumentPosting posting)
        {
            var doc = _docReader.Get(posting.DocumentId);

            doc.Fields["__score"] = posting.Scoring.Score.ToString(CultureInfo.InvariantCulture);

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