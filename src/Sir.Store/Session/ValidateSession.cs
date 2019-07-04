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
        private readonly ProducerConsumerQueue<(long docId, object key, AnalyzedData tokens)> _validator;
        private readonly DocumentComparer _comparer;

        public ValidateSession(
            string collectionName,
            ulong collectionId,
            SessionFactory sessionFactory, 
            IStringModel model,
            IConfigurationProvider config
            ) : base(collectionName, collectionId, sessionFactory)
        {
            _config = config;
            _model = model;
            _validator = new ProducerConsumerQueue<(long docId, object key, AnalyzedData tokens)>(
                10, Validate);
            _comparer = new DocumentComparer();
        }

        public void Validate(IEnumerable<IDictionary> documents)
        {
            foreach (var doc in documents)
            {
                var docId = (long)doc["___docid"];

                foreach (var key in doc.Keys)
                {
                    var strKey = key.ToString();

                    if (!strKey.StartsWith("_"))
                    {
                        var terms = _model.Tokenize(doc[key].ToString());

                        _validator.Enqueue((docId, key, terms));
                    }       
                }
            }
        }

        private void Validate((long docId, object key, AnalyzedData tokens) item)
        {
            var docTree = new VectorNode();

            foreach (var embedding in item.tokens.Embeddings)
            {
                GraphBuilder.Add(docTree, new VectorNode(embedding), _model);
            }

            using (var readSession = new ReadSession(
                CollectionName,
                CollectionId,
                SessionFactory,
                SessionFactory.Config,
                SessionFactory.Model,
                new CollectionStreamReader(CollectionId, SessionFactory)))
            foreach (var node in PathFinder.All(docTree))
            {
                var query = new Query(CollectionId, new Term(item.key, node));
                var result = readSession.Read(query);

                if (!result.Docs.Contains(new Dictionary<string, object> { { "___docid", item.docId } }, _comparer))
                {
                    throw new DataMisalignedException();
                }
            }

            this.Log("validated doc {0}", item.docId);
        }

        public void Dispose()
        {
            _validator.Dispose();
        }
    }

    public class DocumentComparer : IEqualityComparer<IDictionary<string, object>>
    {
        public bool Equals(IDictionary<string, object> x, IDictionary<string, object> y)
        {
            return (long)x["___docid"] == (long)y["___docid"];
        }

        public int GetHashCode(IDictionary<string, object> obj)
        {
            return obj.GetHashCode();
        }
    }
}