using System.Collections.Generic;

namespace Sir.Search
{
    public abstract class Reducer
    {
        protected abstract IList<(ulong, long)> Read(ulong collectionId, IList<long> postingsOffsets);

        public void Reduce(Query mappedQuery, IDictionary<(ulong, long), double> result)
        {
            foreach (var term in mappedQuery.Terms)
            {
                var termResult = Read(term.CollectionId, term.PostingsOffsets);

                if (term.Intersection)
                {
                    if (result.Count == 0)
                    {
                        foreach (var docId in termResult)
                        {
                            result.Add(docId, term.Score);
                        }

                        continue;
                    }

                    var scored = new HashSet<(ulong, long)>();

                    foreach (var docId in termResult)
                    {
                        double score;

                        if (result.TryGetValue(docId, out score))
                        {
                            result[docId] = score + term.Score;

                            scored.Add(docId);
                        }
                    }

                    var bad = new HashSet<(ulong, long)>();

                    foreach (var doc in result)
                    {
                        if (!scored.Contains(doc.Key))
                        {
                            bad.Add(doc.Key);
                        }
                    }

                    foreach (var docId in bad)
                    {
                        result.Remove(docId);
                    }
                }
                else if (term.Union)
                {

                    if (result.Count == 0)
                    {
                        foreach (var docId in termResult)
                        {
                            result.Add(docId, term.Score);
                        }

                        continue;
                    }

                    foreach (var docId in termResult)
                    {
                        double score;

                        if (result.TryGetValue(docId, out score))
                        {
                            result[docId] = score + term.Score;
                        }
                        else
                        {
                            result.Add(docId, term.Score);
                        }
                    }
                }
                else // Not
                {
                    if (result.Count == 0)
                    {
                        continue;
                    }

                    foreach (var docId in termResult)
                    {
                        result.Remove(docId);
                    }
                }
            }

            if (mappedQuery.And != null)
            {
                Reduce(mappedQuery.And, result);
            }
            if (mappedQuery.Or != null)
            {
                Reduce(mappedQuery.Or, result);
            }
            if (mappedQuery.Not != null)
            {
                Reduce(mappedQuery.Not, result);
            }
        }
    }
}