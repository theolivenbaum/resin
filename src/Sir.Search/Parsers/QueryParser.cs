using System.Collections.Generic;

namespace Sir.Store
{
    public class QueryParser
    {
        private readonly SessionFactory _sessionFactory;
        private readonly IStringModel _model;

        public QueryParser(SessionFactory sessionFactory, IStringModel model)
        {
            _sessionFactory = sessionFactory;
            _model = model;
        }

        public IEnumerable<Query> Parse(ulong collectionId, string q, string[] fields, bool and, bool or, bool not)
        {
            var document = new Dictionary<string, object>();

            foreach (var field in fields)
            {
                document.Add(field, q);
            }

            foreach (var field in document)
            {
                long keyId;

                if (_sessionFactory.TryGetKeyId(collectionId, field.Key.ToHash(), out keyId))
                {
                    yield return new Query(keyId, _model.Tokenize((string)field.Value), and, or, not);
                }
            }
        }
    }
}
