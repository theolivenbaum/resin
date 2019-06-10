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
        private readonly IModel _tokenizer;
        private readonly ReadSession _readSession;
        private readonly ProducerConsumerQueue<(long docId, object key, AnalyzedString tokens)> _validator;

        public ValidateSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            IModel tokenizer,
            IConfigurationProvider config
            ) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _readSession = new ReadSession(CollectionName, CollectionId, SessionFactory, _config, tokenizer);
            _validator = new ProducerConsumerQueue<(long docId, object key, AnalyzedString tokens)>(
                int.Parse(_config.Get("write_thread_count")), Validate);
        }

        public void Validate(IEnumerable<IDictionary> documents, params long[] excludeKeyIds)
        {
            foreach (var doc in documents)
            {
                var docId = (long)doc["___docid"];

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

                        _validator.Enqueue((docId, key, terms));
                    }       
                }
            }
        }

        private void Validate((long docId, object key, AnalyzedString tokens) item)
        {
            var docTree = new VectorNode();

            foreach (var vector in item.tokens.Embeddings)
            {
                GraphBuilder.Add(docTree, new VectorNode(vector, item.docId), _tokenizer);
            }

            foreach (var node in PathFinder.All(docTree))
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