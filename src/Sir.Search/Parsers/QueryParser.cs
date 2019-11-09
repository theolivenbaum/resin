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

        public Query Parse(string collectionName, string q, string[] fields, bool and, bool or)
        {
            var root = new Dictionary<string, object>
            {
                { "collection", collectionName },
                { "operator", and ? "and" : or ? "or": "not" }
            };

            if (fields.Length == 1)
            {
                root[fields[0]] = q;
            }
            else
            {
                var cursor = root;

                foreach (var field in fields)
                {
                    cursor[field] = q;

                    var next = new Dictionary<string, object>();

                    if (and)
                    {
                        cursor["and"] = next;
                    }
                    else if (or)
                    {
                        cursor["or"] = next;
                    }
                    else
                    {
                        cursor["not"] = next;
                    }

                    cursor = next;
                }
            }

            return ParseQuery(root);
        }

        public Query ParseQuery(IDictionary<string, object> document)
        {
            Query root = null;
            Query cursor = null;
            var rootCollections = ((string)document["collection"])
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries);

            var operation = document;

            while (operation != null)
            {
                var collectionNames = rootCollections;
                string key = null;
                string value = null;
                object next = null;
                bool and = false;
                bool or = false;
                bool not = false;

                foreach (var kvp in operation)
                {
                    if (kvp.Key == "collection")
                    {
                        collectionNames = ((string)kvp.Value)
                            .Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                    }
                    else if (kvp.Key == "and")
                    {
                        and = true;
                        next = kvp.Value;
                    }
                    else if (kvp.Key == "or")
                    {
                        or = true;
                        next = kvp.Value;
                    }
                    else if (kvp.Key == "not")
                    {
                        not = true;
                        next = kvp.Value;
                    }
                    else if (kvp.Key == "operator")
                    {
                        and = (string)kvp.Value == "and";
                        or = (string)kvp.Value == "or";
                        not = (string)kvp.Value == "not";
                    }
                    else
                    {
                        key = kvp.Key;
                        value = (string)kvp.Value;
                    }
                }

                operation = next as IDictionary<string, object>;

                Query r = null;
                Query c = null;

                foreach (var collection in collectionNames)
                {
                    var q = new Query(ParseTerms(collection, key, value, and, or, not));

                    if (r == null)
                    {
                        r = c = q;
                    }
                    else
                    {
                        c.Or = q;

                        c = q;
                    }
                }

                if (root == null)
                {
                    root = cursor = r;
                }
                else
                {
                    if (and)
                        cursor.And = r;
                    else if (or)
                        cursor.Or = r;
                    else
                        cursor.Not = r;

                    cursor = r;
                }


            }

            return root;
        }

        public IList<Term> ParseTerms(string collectionName, string key, string value, bool and, bool or, bool not)
        {
            var collectionId = collectionName.ToHash();
            long keyId;
            var terms = new List<Term>();

            if (_sessionFactory.TryGetKeyId(collectionId, key.ToHash(), out keyId))
            {
                foreach (var term in _model.Tokenize(value))
                {
                    terms.Add(new Term(collectionId, keyId, key, term, and, or, not));
                }
            }

            return terms;
        }
    }
}