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

            return Parse(collectionId, document, and, or, not);
        }

        public IEnumerable<Query> Parse(ulong collectionId, IDictionary<string, object> document, bool and, bool or, bool not)
        {
            foreach (var field in document)
            {
                long keyId;

                if (_sessionFactory.TryGetKeyId(collectionId, field.Key.ToHash(), out keyId))
                {
                    var clauses = new List<Clause>();

                    foreach (var term in _model.Tokenize((string)field.Value))
                    {
                        clauses.Add(new Clause(keyId, term, and, or, not));
                    }

                    yield return new Query(keyId, clauses);
                }
            }
        }
    }
}