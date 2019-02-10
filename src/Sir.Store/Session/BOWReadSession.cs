using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System;

namespace Sir.Store
{
    /// <summary>
    /// Read session targeting a single collection.
    /// </summary>
    public class BOWReadSession : DocumentSession, ILogger
    {
        private readonly DocIndexReader _docIx;
        private readonly DocMapReader _docs;
        private readonly ValueIndexReader _keyIx;
        private readonly ValueIndexReader _valIx;
        private readonly ValueReader _keyReader;
        private readonly ValueReader _valReader;
        private readonly RemotePostingsReader _postingsReader;
        private readonly ConcurrentDictionary<long, NodeReader> _indexReaders;

        public BOWReadSession(string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            IConfigurationProvider config) 
            : base(collectionName, collectionId, sessionFactory)
        {
            ValueStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", CollectionId)));
            KeyStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", CollectionId)));
            DocStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", CollectionId)));
            ValueIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", CollectionId)));
            KeyIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", CollectionId)));
            DocIndexStream = sessionFactory.CreateAsyncReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", CollectionId)));

            _docIx = new DocIndexReader(DocIndexStream);
            _docs = new DocMapReader(DocStream);
            _keyIx = new ValueIndexReader(KeyIndexStream);
            _valIx = new ValueIndexReader(ValueIndexStream);
            _keyReader = new ValueReader(KeyStream);
            _valReader = new ValueReader(ValueStream);
            _postingsReader = new RemotePostingsReader(config, collectionName);
            _indexReaders = new ConcurrentDictionary<long, NodeReader>();
        }

        public ReadResult Read(IDictionary<long, SortedList<int, byte>> query, ReadSession readSession, int skip, int take)
        {
            return Reduce(query, readSession, skip, take);
        }

        private ReadResult Reduce(IDictionary<long, SortedList<int, byte>> query, ReadSession readSession, int skip, int take)
        {
            throw new NotImplementedException();

            //IDictionary<long, Hit> scored = null;

            //foreach (var term in query)
            //{
            //    var hits = Scan(term.Key, term.Value).ToDictionary(x => x.PostingsOffset, y => y);

            //    if (scored == null)
            //    {
            //        scored = hits;
            //    }
            //    else
            //    {
            //        foreach (var hit in hits)
            //        {
            //            Hit score;

            //            if (scored.TryGetValue(hit.Value.PostingsOffset, out score))
            //            {
            //                scored[hit.Key].Score = score.Score + hit.Value.Score;
            //            }
            //            else
            //            {
            //                scored.Add(hit.Key, hit.Value);
            //            }
            //        }
            //    }
            //}

            //var sortedHits = scored.Values.OrderByDescending(h => h.Score);
            //var offsets = sortedHits.Select(h => h.PostingsOffset).ToArray();
            //var docIds = _postingsReader.Read(skip, take, offsets);
            //var window = docIds.GroupBy(x => x).Select(x => (x.Key, x.Count()))
            //    .OrderByDescending(x => x.Item2)
            //    .Skip(skip)
            //    .Take(take)
            //    .Select(x => x.Key).ToList();
            //var docs = readSession.ReadDocs(window);

            //return new ReadResult { Docs = docs, Total = docIds.Count };
        }

        private IList<Hit> Scan(long keyId, SortedList<int, byte> query)
        {
            IEnumerable<Hit> hits = null;

            var indexReader = CreateDocumentIndexReader(keyId);

            if (indexReader != null)
            {
                hits = indexReader.ClosestMatch(query);
            }

            return hits.OrderByDescending(x => x.Score).ToList();
        }

        public NodeReader CreateDocumentIndexReader(long keyId)
        {
            NodeReader reader;

            if (!_indexReaders.TryGetValue(keyId, out reader))
            {
                var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix1", CollectionId, keyId));
                var ixpFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ixp1", CollectionId, keyId));
                var vecFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.vec1", CollectionId));

                IList<(long, long)> pages;
                using (var ixpStream = SessionFactory.CreateAsyncReadStream(ixpFileName))
                {
                    pages = new PageIndexReader(ixpStream).ReadAll();
                }

                reader = new NodeReader(ixFileName, vecFileName, SessionFactory, pages);

                _indexReaders.GetOrAdd(keyId, reader);
            }

            return reader;
        }
    }
}
