using System.Collections.Generic;

namespace Resin.Analysis
{
    public class AnalyzedDocument
    {
        private readonly IList<AnalyzedTerm> _words;

        public IList<AnalyzedTerm> Words { get { return _words; } }

        public int Id { get; private set; }

        public AnalyzedDocument(int id, IList<AnalyzedTerm> words)
        {
            Id = id;
            _words = words;
        }
    }
}