using System.Collections.Generic;

namespace Resin.Analysis
{
    public class AnalyzedDocument
    {
        private readonly IEnumerable<AnalyzedTerm> _words;

        public IEnumerable<AnalyzedTerm> Words { get { return _words; } }

        public int Id { get; private set; }

        public AnalyzedDocument(int id, IEnumerable<AnalyzedTerm> words)
        {
            Id = id;
            _words = words;
        }
    }
}