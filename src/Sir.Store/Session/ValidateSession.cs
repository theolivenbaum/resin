using Sir.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Validate a collection.
    /// </summary>
    public class ValidateSession : CollectionSession, IDisposable, ILogger
    {
        private readonly IConfigurationProvider _config;
        private readonly ITokenizer _tokenizer;
        private readonly ReadSession _readSession;
        private readonly ProducerConsumerQueue<(long docId, IComparable key, AnalyzedString tokens)> _validator;
        private readonly RemotePostingsReader _postingsReader;

        public ValidateSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config,
            ConcurrentDictionary<long, NodeReader> indexReaders) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _readSession = new ReadSession(CollectionName, CollectionId, SessionFactory, _config, indexReaders);
            _validator = new ProducerConsumerQueue<(long docId, IComparable key, AnalyzedString tokens)>(
                int.Parse(_config.Get("write_thread_count")), Validate);
            _postingsReader = new RemotePostingsReader(_config, collectionName);
        }

        public void Validate(IEnumerable<IDictionary> documents, params long[] excludeKeyIds)
        {
            foreach (var doc in documents)
            {
                var docId = (long)doc["__docid"];

                foreach (var key in doc.Keys)
                {
                    var strKey = key.ToString();

                    if (!strKey.StartsWith("__"))
                    {
                        var keyId = SessionFactory.GetKeyId(CollectionId, strKey.ToHash());

                        if (excludeKeyIds.Contains(keyId))
                        {
                            continue;
                        }

                        var terms = _tokenizer.Tokenize(doc[key].ToString());

                        _validator.Enqueue((docId, (IComparable)key, terms));
                    }       
                }
            }
        }

        private void Validate((long docId, AnalyzedString tokens, NodeReader indexReader) item)
        {
            foreach (var vector in item.tokens.Embeddings)
            {
                var hit = item.indexReader.ClosestMatch(vector);
                var postings = new HashSet<long>();
                var offsets = hit.Node.PostingsOffsets == null ? 
                    new[] { hit.Node.PostingsOffset } : hit.Node.PostingsOffsets.ToArray();

                foreach (var id in _postingsReader.Read(0, 0, offsets))
                {
                    postings.Add(id);
                }

                if (!postings.Contains(item.docId))
                {
                    throw new DataMisalignedException();
                }
            }

            this.Log("validated doc {0}", item.docId);
        }

        private void Validate((long docId, IComparable key, AnalyzedString tokens) item)
        {
            var docTree = new VectorNode();

            foreach (var vector in item.tokens.Embeddings)
            {
                docTree.Add(new VectorNode(vector), VectorNode.TermIdenticalAngle, VectorNode.TermFoldAngle);
            }

            foreach (var node in docTree.All())
            {
                var query = new Query(CollectionId, new Term(item.key, node));
                bool valid = false;

                foreach (var id in _readSession.ReadIds(query))
                {
                    if (id == item.docId)
                    {
                        valid = true;
                        break;
                    }
                }

                if (!valid)
                {
                    throw new DataMisalignedException();
                }
            }

            this.Log("**************************validated doc {0}", item.docId);
        }

        public void Dispose()
        {
            _validator.Dispose();
            _readSession.Dispose();
        }
    }
}