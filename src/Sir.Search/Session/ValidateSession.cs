using Sir.KeyValue;
using System;
using System.Collections;
using System.Collections.Generic;

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

        public ValidateSession(
            ulong collectionId,
            SessionFactory sessionFactory, 
            IStringModel model,
            IConfigurationProvider config,
            IPostingsReader postingsReader
            ) : base(collectionId, sessionFactory)
        {
            _config = config;
            _model = model;
            _readSession = new ReadSession(
                CollectionId,
                SessionFactory,
                SessionFactory.Config,
                SessionFactory.Model,
                new DocumentReader(CollectionId, SessionFactory),
                postingsReader);
        }

        public void Validate(IDictionary doc)
        {
            var docId = (long)doc["___docid"];
            var body = (string)doc["body"];
            var keyId = SessionFactory.GetKeyId(CollectionId, "body".ToHash());
            var query = new Query(keyId, _model.Tokenize(body));

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