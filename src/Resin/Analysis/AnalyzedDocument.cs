using System.Collections.Generic;
using Resin.IO;

namespace Resin.Analysis
{
    public class AnalyzedDocument
    {
        private readonly IDictionary<string, LcrsTrie> _fields;

        public IDictionary<string, LcrsTrie> Fields { get { return _fields; } }

        public int Id { get; private set; }

        public AnalyzedDocument(int id, IDictionary<string, LcrsTrie> fields)
        {
            Id = id;
            _fields = fields;
        }
    }
}