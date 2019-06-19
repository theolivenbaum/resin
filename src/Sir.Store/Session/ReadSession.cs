using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Sir.Store
{
    /// <summary>
    /// Read session targeting a single collection.
    /// </summary>
    public class ReadSession : DocumentSession, ILogger
    {
        private readonly RocksDb _db;
        private readonly IConfigurationProvider _config;
        private readonly IStringModel _tokenizer;
        private readonly Stream _postingsView;

        public ReadSession(string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            IConfigurationProvider config,
            IStringModel tokenizer,
            RocksDb db) 
            : base(collectionName, collectionId, sessionFactory)
        {
            ValueStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.val", CollectionId)));
            KeyStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.key", CollectionId)));
            DocStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.docs", CollectionId)));
            ValueIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.vix", CollectionId)));
            KeyIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.kix", CollectionId)));
            DocIndexStream = sessionFactory.CreateReadStream(Path.Combine(sessionFactory.Dir, string.Format("{0}.dix", CollectionId)));

            _db = db;
            _config = config;
            _tokenizer = tokenizer;

            var posFileName = Path.Combine(SessionFactory.Dir, $"{CollectionId}.pos");

            _postingsView = SessionFactory.CreateReadStream(posFileName);
        }

        public override void Dispose()
        {
            _postingsView.Dispose();

            base.Dispose();
        }
        public ReadResult Read(Query query)
        {
            if (SessionFactory.CollectionExists(query.Collection))
            {
                var result = MapReduce(query);

                if (result != null)
                {
                    var docs = ReadDocs(result.SortedDocuments);

                    return new ReadResult { Total = result.Total, Docs = docs };
                }
            }

            this.Log("found nothing for query {0}", query);

            return new ReadResult { Total = 0, Docs = new IDictionary<string, object>[0] };
        }

        public IEnumerable<BigInteger> ReadIds(Query query)
        {
            if (SessionFactory.CollectionExists(query.Collection))
            {
                var result = MapReduce(query);

                if (result == null)
                {
                    this.Log("found nothing for query {0}", query);

                    return new BigInteger[0];
                }

                return new Dictionary<BigInteger, float>(result.SortedDocuments).Keys;
            }

            return new BigInteger[0];
        }

        /// <summary>
        /// Find each query term's corresponding index node and postings list and perform "AND", "OR" or "NOT" set operations on them.
        /// </summary>
        /// <param name="query"></param>
        private ScoredResult MapReduce(Query query)
        {
            Map(query);

            var timer = Stopwatch.StartNew();

            var result = new PostingsReader(_postingsView).Reduce(query.ToClauses(), query.Skip, query.Take);

            this.Log("map/reduce took {0}", timer.Elapsed);

            return result;
        }

        /// <summary>
        /// Map query terms to index IDs.
        /// </summary>
        /// <param name="query">An un-mapped query</param>
        public void Map(Query query)
        {
            var timer = Stopwatch.StartNew();

            var clauses = query.ToClauses();

            //Parallel.ForEach(clauses, q =>
            foreach (var q in clauses)
            {
                var cursor = q;

                while (cursor != null)
                {
                    Hit hit = null;

                    var indexReader = cursor.Term.KeyId.HasValue ?
                        CreateIndexReader(cursor.Term.KeyId.Value) :
                        CreateIndexReader(cursor.Term.KeyHash);

                    if (indexReader != null)
                    {
                        hit = indexReader.ClosestMatch(cursor.Term.Vector, _tokenizer);
                    }

                    if (hit != null && hit.Score > 0)
                    {
                        cursor.Score = hit.Score;

                        foreach (var offs in hit.Node.PostingsOffsets)
                        {
                            cursor.PostingsOffsets.Add(offs);
                        }
                    }

                    cursor = cursor.NextTermInClause;
                }
            }//);

            this.Log("mapping {0} took {1}", query, timer.Elapsed);
        }

        public NodeReader CreateIndexReader(ulong keyId)
        {
            var ixFileName = Path.Combine(SessionFactory.Dir, string.Format("{0}.{1}.ix", CollectionId, keyId));

            if (!File.Exists(ixFileName))
                return null;

            return new NodeReader(CollectionId, keyId, SessionFactory, _config);
        }

        public IList<IDictionary<string, object>> ReadDocs(IEnumerable<KeyValuePair<BigInteger, float>> docs)
        {
            var result = new List<IDictionary<string, object>>();

            foreach (var d in docs)
            {
                var doc = new Dictionary<string, object>();
                doc["___docid"] = d.Key;
                doc["___score"] = d.Value;

                var docId = d.Key.ToByteArray();
                Span<byte> map = _db.Get(docId);
                var columnCount = map.Length / DbKeys.KeyId;
                var valueId = new byte[DbKeys.ValueId];
                
                Buffer.BlockCopy(docId, 0, valueId, 0, docId.Length);

                for (int i = 0; i < columnCount; i++)
                {
                    var keyIdSlice = map.Slice(i * DbKeys.KeyId, DbKeys.KeyId);

                    Buffer.BlockCopy(keyIdSlice.ToArray(), 0, valueId, docId.Length, DbKeys.KeyId);

                    var value = Read(valueId);
                    var keyId = BitConverter.ToUInt64(keyIdSlice);
                    var keyBuf = _db.Get(keyIdSlice.ToArray());
                    var key = System.Text.Encoding.Unicode.GetString(keyBuf);

                    doc.Add(key, value);
                }

                result.Add(doc);
            }

            return result;
        }

        public IList<IDictionary<string, object>> ReadDocs(IEnumerable<BigInteger> docs)
        {
            var result = new List<IDictionary<string, object>>();

            foreach (var d in docs)
            {
                var doc = new Dictionary<string, object>();
                doc["___docid"] = d;
                doc["___score"] = 1f;

                var docId = d.ToByteArray();
                Span<byte> map = _db.Get(docId);
                var count = map.Length / DbKeys.KeyId;
                var valueId = new byte[DbKeys.ValueId];

                Buffer.BlockCopy(docId, 0, valueId, 0, docId.Length);

                for (int i = 0; i < count; i++)
                {
                    var slice = map.Slice(i * DbKeys.KeyId, DbKeys.KeyId);

                    Buffer.BlockCopy(slice.ToArray(), 0, valueId, docId.Length, DbKeys.KeyId);

                    var value = Read(valueId);

                    var keyId = BitConverter.ToUInt64(slice);

                    var key = new string(System.Text.Encoding.Unicode.GetChars(_db.Get(slice.ToArray())));

                    doc.Add(key, value);
                }

                result.Add(doc);
            }

            return result;
        }

        private object Read(byte[] valueId)
        {
            Span<byte> buffer = _db.Get(valueId);
            Span<byte> slice = buffer.Slice(0, buffer.Length - 1);
            var typeId = buffer[buffer.Length - 1];

            if (DataType.BOOL == typeId)
            {
                return BitConverter.ToBoolean(slice);
            }
            else if (DataType.CHAR == typeId)
            {
                return BitConverter.ToChar(slice);
            }
            else if (DataType.FLOAT == typeId)
            {
                return BitConverter.ToSingle(slice);
            }
            else if (DataType.INT == typeId)
            {
                return BitConverter.ToInt32(slice);
            }
            else if (DataType.DOUBLE == typeId)
            {
                return BitConverter.ToDouble(slice);
            }
            else if (DataType.LONG == typeId)
            {
                return BitConverter.ToInt64(slice);
            }
            else if (DataType.DATETIME == typeId)
            {
                return DateTime.FromBinary(BitConverter.ToInt64(slice));
            }
            else if (DataType.STRING == typeId)
            {
                return new string(System.Text.Encoding.Unicode.GetChars(slice.ToArray()));
            }
            else
            {
                return slice.ToArray();
            }
        }
    }
}
