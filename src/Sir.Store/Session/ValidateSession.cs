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

                        Validate(keyId, terms, reader);
                    }
                        
                }

                this.Log("validated doc {0}", docId);
            }
        }

        private void Validate(long keyId, AnalyzedString tokens, NodeReader indexReader)
        {
            foreach (var vector in tokens.Embeddings)
            {
                Hit best = null;

                foreach (var page in indexReader.ReadAllPages())
                {
                    var hit = page.ClosestMatch(vector);

                    if (best == null || hit.Score > best.Score)
                    {
                        best = hit;
                    }
                }

                if (best.Score < VectorNode.IdenticalTermAngle)
                {
                    throw new DataMisalignedException();
                }
            }

            //foreach (var vector in tokens.Embeddings)
            //{
            //    var hit = indexReader.ClosestMatch(vector).OrderBy(x => x.Score).LastOrDefault();

            //    if (hit == null || hit.Score < VectorNode.IdenticalAngle)
            //    {
            //        throw new DataMisalignedException();
            //    }
            //}
        }

        public void Dispose()
        {
            _readSession.Dispose();
        }
    }
}