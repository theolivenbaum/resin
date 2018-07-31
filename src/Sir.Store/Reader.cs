using System.Collections;
using System.Collections.Generic;

namespace Sir.Store
{
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
                    foreach(var model in session.Read(query))
                    {
                        yield return model;
                    }
                }
            }
        }
    }
}
