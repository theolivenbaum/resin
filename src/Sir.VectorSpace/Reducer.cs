using System.Collections.Generic;

namespace Sir.Store
{
    public abstract class Reducer
    {
        protected abstract IList<long> Read(IList<long> postingsOffsets);

        public void Reduce(IEnumerable<Query> mappedQueries, IDictionary<long, double> result)
        {
            foreach (var query in mappedQueries)
            {
                var queryResult = new Dictionary<long, double>();

                foreach (var clause in query.Clauses)
                {
                    var clauseResult = Read(clause.PostingsOffsets);

                    if (clause.And)
                    {
                        if (queryResult.Count == 0)
                        {
                            foreach (var docId in clauseResult)
                            {
                                queryResult.Add(docId, clause.Score);
                            }

                            continue;
                        }

                        var scored = new HashSet<long>();

                        foreach (var docId in clauseResult)
                        {
                            double score;

                            if (queryResult.TryGetValue(docId, out score))
                            {
                                queryResult[docId] = score + clause.Score;

                                scored.Add(docId);
                            }
                        }

                        var bad = new HashSet<long>();

                        foreach (var doc in queryResult)
                        {
                            if (!scored.Contains(doc.Key))
                            {
                                bad.Add(doc.Key);
                            }
                        }

                        foreach (var docId in bad)
                        {
                            queryResult.Remove(docId);
                        }
                    }
                    else if (clause.Not)
                    {
                        if (queryResult.Count == 0)
                        {
                            continue;
                        }

                        foreach (var docId in clauseResult)
                        {
                            queryResult.Remove(docId);
                        }
                    }
                    else // Or
                    {
                        if (queryResult.Count == 0)
                        {
                            foreach (var docId in clauseResult)
                            {
                                queryResult.Add(docId, clause.Score);
                            }

                            continue;
                        }

                        foreach (var docId in clauseResult)
                        {
                            double score;

                            if (queryResult.TryGetValue(docId, out score))
                            {
                                queryResult[docId] = score + clause.Score;
                            }
                            else
                            {
                                queryResult.Add(docId, clause.Score);
                            }
                        }
                    }
                }

                if (query.And)
                {
                    if (result.Count == 0)
                    {
                        foreach (var doc in queryResult)
                        {
                            result.Add(doc.Key, doc.Value);
                        }

                        continue;
                    }

                    var scored = new HashSet<long>();

                    foreach (var doc in queryResult)
                    {
                        double score;

                        if (queryResult.TryGetValue(doc.Key, out score))
                        {
                            queryResult[doc.Key] = score + doc.Value;

                            scored.Add(doc.Key);
                        }
                    }

                    var bad = new HashSet<long>();

                    foreach (var doc in result)
                    {
                        if (!queryResult.ContainsKey(doc.Key))
                        {
                            bad.Add(doc.Key);
                        }
                    }

                    foreach (var docId in bad)
                    {
                        result.Remove(docId);
                    }
                }
                else if (query.Not)
                {
                    if (result.Count == 0)
                    {
                        continue;
                    }

                    foreach (var doc in queryResult)
                    {
                        result.Remove(doc.Key);
                    }
                }
                else // Or
                {
                    if (result.Count == 0)
                    {
                        foreach (var doc in queryResult)
                        {
                            result.Add(doc.Key, doc.Value);
                        }

                        continue;
                    }

                    foreach (var doc in queryResult)
                    {
                        double score;

                        if (result.TryGetValue(doc.Key, out score))
                        {
                            result[doc.Key] = score + doc.Value;
                        }
                        else
                        {
                            result.Add(doc.Key, doc.Value);
                        }
                    }
                }
            }
        }
    }
}