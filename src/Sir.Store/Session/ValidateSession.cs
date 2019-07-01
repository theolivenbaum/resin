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
        private readonly IStringModel _model;
        private readonly ReadSession _readSession;
        private readonly ProducerConsumerQueue<(long docId, object key, AnalyzedData tokens)> _validator;

        public ValidateSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            IStringModel model,
            IConfigurationProvider config
            ) : base(collectionName, collectionId, sessionFactory)
        {
            _readSession = new ReadSession(
                CollectionName,
                CollectionId,
                SessionFactory,
                config,
                model,
                new CollectionStreamReader(collectionId, sessionFactory));

            _config = config;
            _model = model;
            _validator = new ProducerConsumerQueue<(long docId, object key, AnalyzedData tokens)>(
                int.Parse(_config.Get("validate_thread_count")), Validate);
        }

        public void Validate(IEnumerable<IDictionary> documents, params long[] excludeKeyIds)
        {
            foreach (var doc in documents)
            {
                var docId = (long)doc["___docid"];

                foreach (var key in doc.Keys)
                {
                    var strKey = key.ToString();

                    if (!strKey.StartsWith("_"))
                    {
                        var keyId = SessionFactory.GetKeyId(CollectionId, strKey.ToHash());

                        if (excludeKeyIds.Contains(keyId))
                        {
                            continue;
                        }

                        var terms = _model.Tokenize(doc[key].ToString());

                        _validator.Enqueue((docId, key, terms));
                    }       
                }
            }
        }

        private void Validate((long docId, object key, AnalyzedData tokens) item)
        {
            var docTree = new VectorNode();

            foreach (var vector in item.tokens.Embeddings)
            {
                GraphBuilder.Add(docTree, new VectorNode(vector, item.docId), _model);
            }

            foreach (var node in PathFinder.All(docTree.Right))
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