using System.Collections.Generic;

namespace Sir.Store
{
    public abstract class Reducer
    {
        protected abstract IList<long> Read(IList<long> postingsOffsets);

        public void Reduce(IEnumerable<Query> mappedQuery, IDictionary<long, double> result)
        {
            foreach (var q in mappedQuery)
            {
                var termResult = Read(q.PostingsOffsets);

                if (q.And)
                {
                    if (result.Count == 0)
                    {
                        foreach (var docId in termResult)
                        {
                            result.Add(docId, q.Score);
                        }

                        continue;
                    }

                    var scored = new HashSet<long>();

                    foreach (var docId in termResult)
                    {
                        double score;

                        if (result.TryGetValue(docId, out score))
                        {
                            result[docId] = score + q.Score;

                            scored.Add(docId);
                        }
                    }

                    var bad = new HashSet<long>();

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
                else if (q.Not)
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
                else // Or
                {
                    if (result.Count == 0)
                    {
                        foreach (var docId in termResult)
                        {
                            result.Add(docId, q.Score);
                        }

                        continue;
                    }

                    foreach (var docId in termResult)
                    {
                        double score;

                        if (result.TryGetValue(docId, out score))
                        {
                            result[docId] = score + q.Score;
                        }
                        else
                        {
                            result.Add(docId, q.Score);
                        }
                    }
                }
            }
        }
    }
}