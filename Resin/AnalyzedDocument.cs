using System.Collections.Generic;
using Resin.IO;

namespace Resin
{
    public class AnalyzedDocument
    {
        private readonly string _id;
        private readonly IDictionary<Term, int> _terms;

        public IDictionary<Term, int> Terms { get { return _terms; } }
        public string Id { get { return _id; } }

        public AnalyzedDocument(string id, IDictionary<string, IDictionary<string, int>> analyzedTerms)
        {
            _id = id;
            _terms = new Dictionary<Term, int>();
            foreach (var field in analyzedTerms)
            {
                foreach (var term in field.Value)
                {
                    var key = new Term(field.Key, term.Key);
                    int count;
                    if (!_terms.TryGetValue(key, out count))
                    {
                        _terms.Add(key, term.Value);
                    }
                    else
                    {
                        _terms[key] = count + term.Value;
                    }
                }
            }
        }
    }
}