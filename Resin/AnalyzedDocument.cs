using System.Collections.Generic;
using Resin.IO;

namespace Resin
{
    public class AnalyzedDocument
    {
        private readonly string _id;
        private readonly IDictionary<Term, object> _terms;

        public IDictionary<Term, object> Terms { get { return _terms; } }
        public string Id { get { return _id; } }

        public AnalyzedDocument(string id, IDictionary<string, IDictionary<string, object>> analyzedTerms)
        {
            _id = id;
            _terms = new Dictionary<Term, object>();
            foreach (var field in analyzedTerms)
            {
                foreach (var term in field.Value)
                {
                    var key = new Term(field.Key, term.Key);
                    object data;
                    if (!_terms.TryGetValue(key, out data))
                    {
                        _terms.Add(key, term.Value);
                    }
                    else
                    {
                        _terms[key] = (int) data + (int) term.Value;
                    }
                }
            }
        }
    }
}