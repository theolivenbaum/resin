using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private readonly IDictionary<Query, IList<DocumentScore>> _scoreCache;
        private readonly IReadSession _readSession;
        private readonly PostingsReader _postingsReader;

        public Collector(string directory, IReadSession readSession, IScoringSchemeFactory scorerFactory = null)
        {
            _readSession = readSession;
            _directory = directory;
            _scorerFactory = scorerFactory??new TfIdfFactory();
            _scoreCache = new Dictionary<Query, IList<DocumentScore>>();
            _postingsReader = new PostingsReader(
                _readSession.Stream, _readSession.Version.PostingsOffset);
        }

        public DocumentScore[] Collect(IList<QueryContext> query)
        {
            foreach (var clause in query)
            {
                Scan(clause);
            }

            var reduceTime = Stopwatch.StartNew();
            var reduced = query.Reduce().ToArray();

            Log.DebugFormat("reduced query {0} producing {1} scores in {2}", query.ToQueryString(), reduced.Length, reduceTime.Elapsed);

            return reduced;
        }

        private void Scan(QueryContext ctx)
        {
            var time = new Stopwatch();
            time.Start();

            if (ctx.Query is TermQuery)
            {
                new TermSearch(_readSession, _scorerFactory, _postingsReader)
                    .Search(ctx);
            }
            else if (ctx.Query is PhraseQuery)
            {
                if (_readSession.Version.WordPositions)
                {
                    new CBOWSearch(_readSession, _scorerFactory, _postingsReader)
                        .Search(ctx, ((PhraseQuery)ctx.Query).Values);
                }
                else
                {
                    new TermFrequencyPhraseSearch(_readSession, _scorerFactory, _postingsReader)
                        .Search(ctx, ((PhraseQuery)ctx.Query).Values);
                }
            }
            else
            {
                new RangeSearch(_readSession, _scorerFactory, _postingsReader)
                    .Search(ctx, ((RangeQuery)ctx.Query).ValueUpperBound);
            }

            Log.DebugFormat("scanned {0} in {1}", ctx.Query.Serialize(), time.Elapsed);
        }

        public void Dispose()
        {
            _postingsReader.Dispose();
        }
    }
}