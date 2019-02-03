using Sir.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sir.Store
{
    public class BOWWriteSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly ReadSession _readSession;
        private readonly ITokenizer _tokenizer;
        private readonly ProducerConsumerQueue<(long docId, long keyId, object key, IDictionary doc)> _vectorCalculator;
        private readonly ProducerConsumerQueue<(long docId, long keyId, SortedList<int, byte> vector)> _indexWriter;
        private readonly SortedList<long, VectorNode> _newColumns;
        private readonly Stream _documentVectorStream;
        private readonly object _writeSync = new object();

        public BOWWriteSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory,
            IConfigurationProvider config,
            ITokenizer tokenizer) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _readSession = new ReadSession(collectionName, collectionId, sessionFactory, config);
            _tokenizer = tokenizer;
            _indexWriter = new ProducerConsumerQueue<(long docId, long keyId, SortedList<int, byte> vector)>(WriteToMemIndex, 2 * int.Parse(config.Get("index_thread_count")));
            _vectorCalculator = new ProducerConsumerQueue<(long docId, long keyId, object key, IDictionary doc)>(CreateVector, int.Parse(config.Get("index_thread_count")));
            _newColumns = new SortedList<long, VectorNode>();

            var docVecFileName = Path.Combine(SessionFactory.Dir, CollectionId + ".vec1");

            _documentVectorStream = SessionFactory.CreateAppendStream(docVecFileName);
        }

        private void CreateVector((long docId, long keyId, object key, IDictionary doc) item)
        {
            var treeReader = _readSession.CreateIndexReader(item.keyId);
            var docVec = CreateDocumentVector(item.doc[item.key], treeReader, _tokenizer);
            _indexWriter.Enqueue((item.docId, item.keyId, docVec));
        }

        private void WriteToMemIndex((long docId, long keyId, SortedList<int, byte> vector) item)
        {
            VectorNode column;

            if (!_newColumns.TryGetValue(item.keyId, out column))
            {
                lock (_writeSync)
                {
                    if (!_newColumns.TryGetValue(item.keyId, out column))
                    {
                        column = new VectorNode();
                        column.SerializeVector(_documentVectorStream);
                        _newColumns.Add(item.keyId, column);
                    }
                }
            }

            column.Add(new VectorNode(item.vector, item.docId), VectorNode.DocIdenticalAngle, VectorNode.DocFoldAngle, _documentVectorStream);

            this.Log("added doc field {0}.{1} to memory index", item.docId, item.keyId);
        }

        public void Write(IEnumerable<IDictionary> documents, params long[] excludeKeyIds)
        {
            // create document embeddings

            foreach (var doc in documents)
            {
                var docId = (long)doc["__docid"];

                foreach (var key in doc.Keys)
                {
                    var strKey = key.ToString();

                    if (strKey.StartsWith("__"))
                    {
                        continue;
                    }

                    var keyId = SessionFactory.GetKeyId(strKey.ToHash());

                    if (excludeKeyIds.Contains(keyId))
                    {
                        continue;
                    }

                    _vectorCalculator.Enqueue((docId, keyId, key, doc));
                }
            }
        }

        public static SortedList<int, byte> CreateDocumentVector(
            object value, NodeReader treeReader, ITokenizer tokenizer)
        {
            var docVec = new SortedList<int, byte>();
            var terms = tokenizer.Tokenize(value.ToString());

            foreach (var vector in terms.Embeddings)
            {
                Hit best = null;

                foreach (var page in treeReader.ReadAllPages())
                {
                    var hit = page.ClosestMatch(new VectorNode(vector), VectorNode.DocFoldAngle);

                    if (best == null || hit.Score > best.Score)
                    {
                        best = hit;
                    }
                }

                var termId = best.NodeId;

                if (!docVec.ContainsKey(termId))
                {
                    docVec.Add(termId, 1);
                }
            }

            return docVec;
        }

        private void Flush()
        {
            _vectorCalculator.Dispose();
            _indexWriter.Dispose();
            _documentVectorStream.Dispose();

            var tasks = new List<Task>();
            var writers = new List<ColumnSerializer>();

            foreach (var model in _newColumns)
            {
                var columnWriter = new ColumnSerializer(CollectionId, model.Key, SessionFactory, new RemotePostingsWriter(_config), "ix1", "ixp1");
                tasks.Add(columnWriter.SerializeColumnSegment(model.Value));
                writers.Add(columnWriter);
            }

            Task.WaitAll(tasks.ToArray());

            foreach (var writer in writers)
            {
                writer.Dispose();
            }

            this.Log("***FLUSHED*** and completed building of model {0}", CollectionId);
        }

        public void Dispose()
        {
            _readSession.Dispose();

            Flush();
        }
    }
}