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

        public IEnumerable<Query> Parse(string collectionName, string q, string[] fields, bool and, bool or, bool not)
        {
            foreach (var field in fields)
            {
                yield return Parse(field, q, and, or, not, collectionName);
            }
        }

        public IEnumerable<Query> Parse(IDictionary<string, object> document)
        {
            var dand = document.ContainsKey("operator") ? document["operator"].Equals("and") : false;
            var dor = document.ContainsKey("operator") ? document["operator"].Equals("or") : false;
            var dnot = document.ContainsKey("operator") ? document["operator"].Equals("not") : false;

            foreach (var termDoc in (IEnumerable<Dictionary<string, object>>)document["terms"])
            {
                var key = (string)termDoc["key"];
                var value = (string)termDoc["value"];
                var operatorString = termDoc.ContainsKey("operator") ? (string)termDoc["operator"] : "or";
                var and = termDoc.ContainsKey("operator") ? termDoc["operator"].Equals("and") : false;
                var or = termDoc.ContainsKey("operator") ? termDoc["operator"].Equals("or") : false;
                var not = termDoc.ContainsKey("operator") ? termDoc["operator"].Equals("not") : false;

                var query = Parse(key, value, and, or, not);

                query.And = dand;
                query.Or = dor;
                query.Not = dnot;

                yield return query;
            }
        }

        public Query Parse(string key, string value, bool and, bool or, bool not, string collectionName = null)
        {
            var keySegments = key.Split('.', System.StringSplitOptions.RemoveEmptyEntries);
            var collectionId = collectionName == null ? keySegments[0].ToHash() : collectionName.ToHash();
            var keyName = keySegments[1];
            long keyId;
            var terms = new List<Term>();

            if (_sessionFactory.TryGetKeyId(collectionId, keyName.ToHash(), out keyId))
            {
                foreach (var term in _model.Tokenize(value))
                {
                    terms.Add(new Term(collectionId, keyId, key, term, and, or, not));
                }
            }

            return new Query(terms, and, or, not);
        }
    }
}