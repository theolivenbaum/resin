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

        private readonly SessionFactory _sessionFactory;
        private readonly ITokenizer _tokenizer;

        public Reader(SessionFactory sessionFactory, ITokenizer analyzer)
        {
            _tokenizer = analyzer;
            _sessionFactory = sessionFactory;
        }

        public void Dispose()
        {
        }

        public IEnumerable<IDictionary> Read(Query query)
        {
            ulong keyHash = query.Term.Key.ToString().ToHash();
            uint keyId;

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
