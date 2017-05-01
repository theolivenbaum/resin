using System.Collections.Generic;
using Resin.IO;

namespace Resin.Analysis
{
    public class AnalyzedDocument
    {
        private readonly IDictionary<Term, DocumentPosting> _words;

        public IDictionary<Term, DocumentPosting> Words { get { return _words; } }

        public int Id { get; private set; }

        public AnalyzedDocument(int id, IDictionary<Term, DocumentPosting> words)
        {
            Id = id;
            _words = words;
        }
    }
}