using Sir.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        private readonly ProducerConsumerQueue<(long docId, object key, AnalyzedString tokens)> _validator;
        private readonly RemotePostingsReader _postingsReader;

        public ValidateSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            ITokenizer tokenizer,
            IConfigurationProvider config
            ) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _tokenizer = tokenizer;
            _readSession = new ReadSession(CollectionName, CollectionId, SessionFactory, _config);
            _validator = new ProducerConsumerQueue<(long docId, object key, AnalyzedString tokens)>(
                int.Parse(_config.Get("write_thread_count")), callback: Validate);
            _postingsReader = new RemotePostingsReader(_config, collectionName);
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

        private async Task Validate((long docId, object key, AnalyzedString tokens) item)
        {
            var docTree = new VectorNode();

            foreach (var vector in item.tokens.Embeddings)
            {
                VectorNodeWriter.Add(docTree, new VectorNode(vector), Similarity.Term);
            }

            foreach (var node in VectorNodeReader.All(docTree))
            {
                var query = new Query(CollectionId, new Term(item.key, node));
                bool valid = false;

                foreach (var id in await _readSession.ReadIds(query))
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