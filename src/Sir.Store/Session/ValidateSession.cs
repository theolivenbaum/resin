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
        private readonly DocumentComparer _comparer;
        private readonly ReadSession _readSession;

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
            _comparer = new DocumentComparer();
            _readSession = new ReadSession(
                CollectionName,
                CollectionId,
                SessionFactory,
                SessionFactory.Config,
                SessionFactory.Model,
                new CollectionStreamReader(CollectionId, SessionFactory));
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

                        Validate(docId, key, terms);
                    }       
                }
            }
        }

        private void Validate(long docId, object key, AnalyzedData tokens)
        {
            var docTree = new VectorNode();

            foreach (var embedding in tokens.Embeddings)
            {
                GraphBuilder.Add(docTree, new VectorNode(embedding), _model);
            }

            foreach (var node in PathFinder.All(docTree))
            {
                var query = new Query(CollectionId, new Term(key, node));
                var result = _readSession.Read(query);

                if (!result.Docs.Contains(new Dictionary<string, object> { { "___docid", docId } }, _comparer))
                {
                    this.Log($"failed to validate node {node} from doc {docId}");

                    throw new DataMisalignedException();
                }
            }

            this.Log("validated doc {0}", docId);
        }

        public void Dispose()
        {
            _readSession.Dispose();
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