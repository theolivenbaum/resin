using System.Collections.Generic;

namespace Sir.Search
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

        public Query Parse(string collection, string q, string[] fields, bool and, bool or)
        {
            var root = new Dictionary<string, object>();
            var cursor = new Dictionary<string, object>
            {
                {"collection", collection }
            };

            if (and)
            {
                root["and"] = cursor;
            }
            else if (or)
            {
                root["or"] = cursor;
            }
            else
            {
                root["not"] = cursor;
            }

            if (fields.Length == 1)
            {
                cursor[fields[0]] = q;
            }
            else
            {
                foreach (var field in fields)
                {
                    cursor[field] = q;

                    var next = new Dictionary<string, object>
                    {
                        {"collection", collection }
                    };

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
            string[] parentCollections = null;
            string op = null;
            var operation = document;

            while (operation != null)
            {
                string[] collections = null;
                string key = null;
                string value = null;
                object next = null;

                foreach (var kvp in operation)
                {
                    if (kvp.Key == "collection")
                    {
                        collections = ((string)kvp.Value)
                            .Split(',', System.StringSplitOptions.RemoveEmptyEntries);

                        if (parentCollections == null)
                            parentCollections = collections;
                    }
                    else if (kvp.Key == "and")
                    {
                        op = "and";
                        next = kvp.Value;
                    }
                    else if (kvp.Key == "or")
                    {
                        op = "or";
                        next = kvp.Value;
                    }
                    else if (kvp.Key == "not")
                    {
                        op = "not";
                        next = kvp.Value;
                    }
                    else
                    {
                        key = kvp.Key;
                        value = (string)kvp.Value;
                    }
                }

                operation = next as IDictionary<string, object>;

                if (key == null)
                    continue;

                Query r = null;
                Query c = null;
                bool and = op == "and";
                bool or = op == "or";
                bool not = op == "not";

                foreach (var collection in collections)
                {
                    var q = new Query(ParseTerms(collection, key, value, and, or, not), and, or, not);

                    if (r == null)
                    {
                        r = c = q;
                    }
                    else
                    {
                        c.OrQuery = q;

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
                        cursor.AndQuery = r;
                    else if (or)
                        cursor.OrQuery = r;
                    else
                        cursor.NotQuery = r;

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