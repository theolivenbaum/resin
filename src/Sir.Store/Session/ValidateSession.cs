using Sir.Core;
using System;
using System.Collections;
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
        private readonly ProducerConsumerQueue<(long docId, AnalyzedString tokens, NodeReader indexReader)> _validator;
        private readonly RemotePostingsReader _postingsReader;

        public ValidateSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _readSession = new ReadSession(CollectionName, CollectionId, SessionFactory, _config);
            _validator = new ProducerConsumerQueue<(long docId, AnalyzedString tokens, NodeReader indexReader)>(Validate, 8);
            _postingsReader = new RemotePostingsReader(_config);
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
                        var keyId = SessionFactory.GetKeyId(strKey.ToHash());

                        if (excludeKeyIds.Contains(keyId))
                        {
                            continue;
                        }

                        var terms = _tokenizer.Tokenize(doc[key].ToString());
                        var reader = _readSession.CreateIndexReader(keyId);

                        _validator.Enqueue((docId, terms, reader));
                    }       
                }
            }
        }

        private void Validate((long docId, AnalyzedString tokens, NodeReader indexReader) item)
        {
            foreach (var vector in item.tokens.Embeddings)
            {
                var hits = new SortedList<float, Hit>(); ;

                foreach (var page in item.indexReader.ReadAllPages())
                {
                    var hit = page.ClosestMatch(new VectorNode(vector), VectorNode.TermFoldAngle);

                    hits.Add(hit.Score, hit);
                }

                if (hits.Keys[0] < VectorNode.TermIdenticalAngle)
                {
                    throw new DataMisalignedException();
                }

                var postings = new HashSet<long>();

                foreach (var hit in hits)
                {
                    foreach(var id in _postingsReader.Read(CollectionName, 0, 0, hit.Value.PostingsOffset))
                    {
                        postings.Add(id);
                    }
                }

                if (!postings.Contains(item.docId))
                {
                    throw new DataMisalignedException();
                }
            }

            this.Log("validated doc {0}", item.docId);
        }

        public void Dispose()
        {
            _validator.Dispose();
            _readSession.Dispose();
        }
    }
}