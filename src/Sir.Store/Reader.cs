using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sir.Store
{
    /// <summary>
    /// Read from the document store.
    /// </summary>
    public class Reader : IReader
    {
        public string ContentType => "*";

        private readonly LocalStorageSessionFactory _sessionFactory;

        public Reader(LocalStorageSessionFactory sessionFactory)
        {
            _sessionFactory = sessionFactory;
        }

        public void Dispose()
        {
        }

        public IEnumerable<IDictionary> Read(Query query)
        {
            ulong keyHash = query.Term.Key.ToString().ToHash();
            long keyId;

            if (_sessionFactory.TryGetKeyId(keyHash, out keyId))
            {
                using (var session = _sessionFactory.CreateReadSession(query.CollectionId))
                {
                    return session.Read(query).ToList();
                }
            }

            return Enumerable.Empty<IDictionary>();
        }
    }
}
