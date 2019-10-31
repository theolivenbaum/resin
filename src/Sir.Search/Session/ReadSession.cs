using Sir.Document;
using Sir.VectorSpace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Read session targeting a single collection.
    /// </summary>
    public class ReadSession : CollectionSession, ILogger, IDisposable
    {
        private readonly IConfigurationProvider _config;
        private readonly IStringModel _model;
        private readonly IPostingsReader _postingsReader;
        private readonly MemoryMappedViewAccessor _vectorView;
        private readonly DocumentReader _streamReader;
        private readonly ConcurrentDictionary<long, INodeReader> _nodeReaders;

        public ReadSession(ulong collectionId,
            SessionFactory sessionFactory, 
            IConfigurationProvider config,
            IStringModel model,
            DocumentReader streamReader,
            IPostingsReader postingsReader) 
            : base(collectionId, sessionFactory)
        {
            _config = config;
            _model = model;
            _streamReader = streamReader;
            _postingsReader = postingsReader;

            _vectorView = SessionFactory.OpenMMF(Path.Combine(SessionFactory.Dir, $"{CollectionId}.vec"))
                .CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            _nodeReaders = new ConcurrentDictionary<long, INodeReader>();
        }

        public void Dispose()
        {
            _vectorView.Dispose();

            _postingsReader.Dispose();

            _streamReader.Dispose();

            foreach (var reader in _nodeReaders.Values)
                reader.Dispose();
        }

        public ReadResult Read(IEnumerable<Query> query, int skip, int take)
        {
            var result = MapReduce(query, skip, take);

            if (result != null)
            {
                var docs = ReadDocs(result.SortedDocuments);

                this.Log("end read session for query {0}", query);

                return new ReadResult { Total = result.Total, Docs = docs };
            }

            this.Log("zero results for query {0}", query);

            return new ReadResult { Total = 0, Docs = new IDictionary<string, object>[0] };
        }

        public void EnsureIsValid(Query query, long docId)
        {
            foreach (var clause in query.Clauses)
            {
                var indexReader = GetOrTryCreateIndexReader(clause.KeyId);

                if (indexReader != null)
                {
                    var hit = indexReader.ClosestTerm(clause.Term, _model);

                    if (hit == null || hit.Score < _model.IdenticalAngle)
                    {
                        throw new DataMisalignedException($"\"{clause}\" not found.");
                    }

                    var docIds = _postingsReader.ReadWithScore(hit.Node.PostingsOffsets, _model.IdenticalAngle);

                    if (!docIds.ContainsKey(docId))
                    {
                        throw new DataMisalignedException(
                            $"document {docId} not found in postings list for term \"{clause}\".");
                    }
                }
            }
        }

        private ScoredResult MapReduce(IEnumerable<Query> query, int skip, int take)
        {
            var mapped = Map(query).ToList();

            var timer = Stopwatch.StartNew();

            var result = new Dictionary<long, double>();

            _postingsReader.Reduce(mapped, result);

            this.Log("reducing took {0}", timer.Elapsed);

            timer.Restart();

            var sorted = Sort(result, skip, take);

            this.Log("sorting took {0}", timer.Elapsed);

            return sorted;
        }

        /// <summary>
        /// Map query terms to index IDs.
        /// </summary>
        /// <param name="query">An un-mapped query</param>
        public IEnumerable<Query> Map(IEnumerable<Query> queries)
        {
            var timer = Stopwatch.StartNew();

            foreach (var query in queries)
            {
                foreach (var clause in query.Clauses)
                {
                    var indexReader = GetOrTryCreateIndexReader(clause.KeyId);

                    if (indexReader != null)
                    {
                        var hit = indexReader.ClosestTerm(clause.Term, _model);

                        if (hit != null && hit.Score > 0)
                        {
                            clause.Score = hit.Score;
                            clause.PostingsOffsets = hit.Node.PostingsOffsets;
                        }
                    }
                }

                yield return query;
            }

            this.Log($"mapping took {timer.Elapsed}");
        }

        private static ScoredResult Sort(IDictionary<long, double> documents, int skip, int take)
        {
            var sortedByScore = new List<KeyValuePair<long, double>>(documents);

            sortedByScore.Sort(
                delegate (KeyValuePair<long, double> pair1,
                KeyValuePair<long, double> pair2)
                {
                    return pair2.Value.CompareTo(pair1.Value);
                }
            );

            var index = skip > 0 ? skip : 0;
            var count = Math.Min(sortedByScore.Count, take > 0 ? take : sortedByScore.Count);

            return new ScoredResult { SortedDocuments = sortedByScore.GetRange(index, count), Total = sortedByScore.Count };
        }

        public INodeReader GetOrTryCreateIndexReader(long keyId)
        {
            var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId, keyId));

            if (!File.Exists(ixFileName))
                return null;

            return _nodeReaders.GetOrAdd(
                keyId, new MemoryMappedNodeReader(CollectionId, keyId, SessionFactory, _config, _vectorView));
        }

        public IList<IDictionary<string, object>> ReadDocs(IEnumerable<KeyValuePair<long, double>> docs)
        {
            var result = new List<IDictionary<string, object>>();

            foreach (var d in docs)
            {
                var docInfo = _streamReader.GetDocumentAddress(d.Key);

                if (docInfo.offset < 0)
                {
                    continue;
                }

                var docMap = _streamReader.GetDocumentMap(docInfo.offset, docInfo.length);
                var doc = new Dictionary<string, object>();

                for (int i = 0; i < docMap.Count; i++)
                {
                    var kvp = docMap[i];
                    var kInfo = _streamReader.GetAddressOfKey(kvp.keyId);
                    var vInfo = _streamReader.GetAddressOfValue(kvp.valId);
                    var key = _streamReader.GetKey(kInfo.offset, kInfo.len, kInfo.dataType);
                    var val = _streamReader.GetValue(vInfo.offset, vInfo.len, vInfo.dataType);

                    doc[key.ToString()] = val;
                }

                doc["___docid"] = d.Key;
                doc["___score"] = d.Value;

                result.Add(doc);
            }

            return result;
        }
    }
}
