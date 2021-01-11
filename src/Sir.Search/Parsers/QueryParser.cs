﻿using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Sir.VectorSpace;
using System.Collections.Generic;

namespace Sir.Search
{
    public class QueryParser<T>
    {
        private readonly StreamFactory _sessionFactory;
        private readonly IModel<T> _model;
        private readonly ILogger _log;
        private readonly string _directory;

        public QueryParser(string directory, StreamFactory sessionFactory, IModel<T> model, ILogger log = null)
        {
            _sessionFactory = sessionFactory;
            _model = model;
            _log = log;
            _directory = directory;
        }

        public Query Parse(
            string collection,
            T q,
            string field,
            string select,
            bool and,
            bool or)
        {
            return Parse(
                new string[] { collection },
                q,
                new string[] { field },
                new string[] { select },
                and,
                or);
        }

        public Query Parse(
            IEnumerable<string> collections,
            T q, 
            string[] fields, 
            IEnumerable<string> select, 
            bool and, 
            bool or)
        {
            var root = new Dictionary<string, object>();
            var cursor = root;

            foreach (var collection in collections)
            {
                var query = new Dictionary<string, object>
                {
                    {"collection", collection }
                };

                if (and)
                {
                    cursor["and"] = query;
                }
                else if (or)
                {
                    cursor["or"] = query;
                }
                else
                {
                    cursor["not"] = query;
                }

                if (fields.Length == 1)
                {
                    query[fields[0]] = q;
                }
                else
                {
                    for (int i = 0; i < fields.Length; i++)
                    {
                        query[fields[i]] = q;

                        if (i < fields.Length - 1)
                        {
                            var next = new Dictionary<string, object>
                            {
                                {"collection", collection }
                            };

                            if (and)
                            {
                                query["and"] = next;
                            }
                            else if (or)
                            {
                                query["or"] = next;
                            }
                            else
                            {
                                query["not"] = next;
                            }

                            query = next;
                        }
                    }
                }

                cursor = query;
            }

            if (_log != null)
            {
                var queryLog = JsonConvert.SerializeObject(root);
                _log.LogDebug($"parsed query: {queryLog}");
            }

            return Parse(root, select);
        }

        public Query Parse(dynamic document, IEnumerable<string> select)
        {
            Query root = null;
            Query cursor = null;
            string[] parentCollections = null;
            bool and = false;
            bool or = false;
            bool not = false;
            var operation = document;

            while (operation != null)
            {
                string[] collections = null;
                var kvps = new List<(string key, T value)>();
                dynamic next = null;

                foreach (var kvp in operation)
                {
                    if (kvp.Key == "collection")
                    {
                        collections = ((string)kvp.Value)
                            .Split(',', System.StringSplitOptions.RemoveEmptyEntries);

                        parentCollections = collections;

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
                    else
                    {
                        var keys = ((string)kvp.Key).Split(',', System.StringSplitOptions.RemoveEmptyEntries);

                        foreach (var k in keys)
                            kvps.Add((k, kvp.Value));
                    }
                }

                operation = next;

                if (kvps.Count == 0)
                {
                    continue;
                }
                else
                {
                    foreach (var collection in collections ?? parentCollections)
                    {
                        foreach (var kvp in kvps)
                        {
                            var terms = ParseTerms(collection, kvp.key, kvp.value, and, or, not);

                            if (terms.Count == 0)
                            {
                                continue;
                            }

                            var query = new Query(terms, select, and, or, not);

                            if (root == null)
                            {
                                root = cursor = query;
                            }
                            else
                            {
                                cursor.Or = query;

                                cursor = query;
                            }
                        }
                    }
                }
            }
                
            return root;
        }

        private IList<Term> ParseTerms(string collectionName, string key, T value, bool and, bool or, bool not)
        {
            var collectionId = collectionName.ToHash();
            long keyId;
            var terms = new List<Term>();

            if (_sessionFactory.TryGetKeyId(_directory, collectionId, key.ToHash(), out keyId))
            {
                var tokens = _model.Tokenize(value);

                foreach (var term in tokens)
                {
                    terms.Add(new Term(_directory, collectionId, keyId, key, term, and, or, not));
                }
            }

            return terms;
        }
    }
}