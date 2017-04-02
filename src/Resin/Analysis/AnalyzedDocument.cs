using System.Collections.Generic;
using Resin.IO;

namespace Resin.Analysis
{
    public class AnalyzedDocument
    {
        private readonly IDictionary<Term, DocumentPosting> _terms;

        public IDictionary<Term, DocumentPosting> Terms { get { return _terms; } } 

        public AnalyzedDocument(int id, IEnumerable<KeyValuePair<string, IDictionary<string, int>>> analyzedTerms)
        {
            _terms = new Dictionary<Term, DocumentPosting>();

            foreach (var field in analyzedTerms)
            {
                foreach (var term in field.Value)
                {
                    _terms.Add(
                        new Term(field.Key, new Word(term.Key)), 
                        new DocumentPosting(id, term.Value));
                }
            }
        }
    }
}