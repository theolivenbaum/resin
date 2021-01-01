using System.Collections.Generic;

namespace Sir.VectorSpace
{
    public abstract class Reducer
    {
        protected abstract IList<(ulong, long)> Read(ulong collectionId, long keyId, IList<long> postingsOffsets);

        public void Map(Query query)
        {
            Map(query.Terms);

            if (query.And != null)
            {
                Map(query.And);
            }
            if (query.Or != null)
            {
                Map(query.Or);
            }
            if (query.Not != null)
            {
                Map(query.Not);
            }
        }

        public void Map(Term term)
        {
            if (term.PostingsOffsets == null)
                return;

            term.Result = Read(term.CollectionId, term.KeyId, term.PostingsOffsets);
        }

        private void Map(IList<Term> terms)
        {
            foreach (var term in terms)
            {
                Map(term);
            }
        }

        public void Reduce(Query query, ref IDictionary<(ulong, long), double> result)
        {
            IDictionary<(ulong, long), double> queryResult = new Dictionary<(ulong, long), double>();

            Reduce(query.Terms, ref queryResult);

            if (query.IsIntersection)
            {
                if (result.Count == 0)
                {
                    foreach (var docId in queryResult)
                    {
                        result.Add(docId.Key, docId.Value);
                    }
                }
                else
                {
                    var intersection = new Dictionary<(ulong, long), double>();

                    foreach (var doc in queryResult)
                    {
                        double score;

                        if (result.TryGetValue(doc.Key, out score))
                        {
                            intersection.Add(doc.Key, score + doc.Value);
                        }
                    }

                    result = intersection;
                }
            }
            else if (query.IsUnion)
            {
                if (result.Count == 0)
                {
                    foreach (var docId in queryResult)
                    {
                        result.Add(docId.Key, docId.Value);
                    }
                }
                else
                {
                    foreach (var doc in queryResult)
                    {
                        if (result.ContainsKey(doc.Key))
                        {
                            result[doc.Key] += doc.Value;
                        }
                    }
                }
            }
            else // Not
            {
                if (result.Count > 0)
                {
                    foreach (var docId in queryResult)
                    {
                        result.Remove(docId.Key);
                    }
                }
            }

            if (query.And != null)
            {
                Reduce(query.And, ref result);
            }
            if (query.Or != null)
            {
                Reduce(query.Or, ref result);
            }
            if (query.Not != null)
            {
                Reduce(query.Not, ref result);
            }
        }

        public void Reduce(Term term, ref IDictionary<(ulong, long), double> result)
        {
            if (term.PostingsOffsets == null)
                return;

            if (term.IsIntersection)
            {
                if (result.Count == 0)
                {
                    foreach (var docId in term.Result)
                    {
                        result.Add(docId, term.Score);
                    }
                }
                else
                {
                    var intersection = new Dictionary<(ulong, long), double>();

                    foreach (var doc in term.Result)
                    {
                        double score;

                        if (result.TryGetValue(doc, out score))
                        {
                            intersection.Add(doc, score + term.Score);
                        }
                    }

                    result = intersection;
                }
            }
            else if (term.IsUnion)
            {
                if (result.Count == 0)
                {
                    foreach (var docId in term.Result)
                    {
                        result.Add(docId, term.Score);
                    }
                }
                else
                {
                    foreach (var doc in term.Result)
                    {
                        if (result.ContainsKey(doc))
                        {
                            result[doc] += term.Score;
                        }
                    }
                }
            }
            else // Not
            {
                if (result.Count > 0)
                {
                    foreach (var doc in term.Result)
                    {
                        result.Remove(doc);
                    }
                }
            }
        }

        private void Reduce(IList<Term> terms, ref IDictionary<(ulong Key, long Value), double> result)
        {
            foreach (var term in terms)
            {
                Reduce(term, ref result);
            }
        }
    }
}