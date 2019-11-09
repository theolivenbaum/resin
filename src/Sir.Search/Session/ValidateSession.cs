using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Validate a collection.
    /// </summary>
    public class ValidateSession : IDisposable, ILogger
    {
        public ulong CollectionId { get; }

        private readonly SessionFactory _sessionFactory;
        private readonly IConfigurationProvider _config;
        private readonly IStringModel _model;
        private readonly ReadSession _readSession;

        public ValidateSession(
            ulong collectionId,
            SessionFactory sessionFactory, 
            IStringModel model,
            IConfigurationProvider config,
            IPostingsReader postingsReader
            )
        {
            CollectionId = collectionId;

            _sessionFactory = sessionFactory;
            _config = config;
            _model = model;
            _readSession = new ReadSession(
                sessionFactory,
                sessionFactory.Config,
                sessionFactory.Model,
                postingsReader);
        }

        public void Validate(IDictionary doc)
        {
            var docId = (long)doc["___docid"];
            var body = (string)doc["body"];
            var keyId = _sessionFactory.GetKeyId(CollectionId, "body".ToHash());
            var query = new Query(
                _model.Tokenize(body)
                    .Select(x => new Term(CollectionId, keyId, "body", x, and:true, or:false, not:false)).ToList(), 
                and:true, 
                or:false, 
                not:false);

            _readSession.EnsureIsValid(query, docId);
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
            return obj["___docid"].GetHashCode();
        }
    }
}