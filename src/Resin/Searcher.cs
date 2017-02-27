using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    public class Searcher : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Searcher));
        private readonly string _directory;
        private readonly QueryParser _parser;
        private readonly IScoringScheme _scorer;
        private readonly IDictionary<string, IxInfo> _indices;

        public Searcher(string directory, QueryParser parser, IScoringScheme scorer)
        {
            _directory = directory;
            _parser = parser;
            _scorer = scorer;

            var ixFiles = GetIndexFileNamesInChronologicalOrder();
            _indices = ixFiles.Select(IxInfo.Load).ToDictionary(x => x.Name);
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
            var docs = paged.Select(GetDoc);

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
                .Aggregate(DocumentPosting.JoinOr)
                .OrderByDescending(p => p.Scoring.Score).ToList();
        }

        private Document GetDoc(DocumentPosting posting)
        {
            var fileId = posting.DocumentId.ToDocFileId();
            var fileName = Path.Combine(_directory, string.Format("{0}-{1}.doc", posting.IndexName, fileId));
            var fs = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var sr = new StreamReader(fs, Encoding.Unicode);

            using (var reader = new DocumentReader(sr))
            {
                var doc = reader.Get(posting.DocumentId);

                doc.Fields["__score"] = posting.Scoring.Score.ToString(CultureInfo.InvariantCulture);

                return doc;    
            }
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