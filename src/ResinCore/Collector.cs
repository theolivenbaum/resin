using System;
using System.Collections.Generic;
using System.Diagnostics;
using log4net;
using Resin.Querying;
using Resin.IO;
using DocumentTable;
using Resin.Analysis;

namespace Resin
{
    public class Collector : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Collector));
        private readonly string _directory;
        private readonly IScoringSchemeFactory _scorerFactory;
        private readonly IFullTextReadSession _readSession;

        public Collector(string directory, IFullTextReadSession readSession, IScoringSchemeFactory scorerFactory = null)
        {
            _readSession = readSession;
            _directory = directory;
            _scorerFactory = scorerFactory??new TfIdfFactory();
        }

        public IList<DocumentScore> Collect(IList<QueryContext> query)
        {
            foreach (var clause in query)
            {
                Scan(clause);
            }

            var reduceTime = Stopwatch.StartNew();
            var reduced = query.Reduce();

            Log.DebugFormat("reduced query {0} producing {1} scores in {2}", 
                query.ToQueryString(), reduced.Count, reduceTime.Elapsed);

            return reduced;
        }

        private void Scan(QueryContext ctx)
        {
            var rangeQ = ctx.Query as PhraseQuery;

            if (rangeQ != null && rangeQ.Value == null)
            {
                new CBOWSearch(_readSession, _scorerFactory)
                    .Search(ctx);
            }
            else if (ctx.Query is RangeQuery)
            {
                new RangeSearch(_readSession, _scorerFactory)
                    .Search(ctx, ((RangeQuery)ctx.Query).ValueUpperBound);
            }
            else
            {
                new TermSearch(_readSession, _scorerFactory)
                    .Search(ctx);
            }
        }

        public void Dispose()
        {
        }
    }
}