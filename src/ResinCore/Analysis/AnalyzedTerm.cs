using System.Collections.Generic;

namespace Resin.Analysis
{
    public class AnalyzedTerm
    {
        public string Field { get; private set; }
        public readonly string Value;
        public IList<int> Positions { get; private set; }
        public int DocumentId { get; set; }

        public AnalyzedTerm(int documentId, string key, string value, IList<int> positions)
        {
            DocumentId = documentId;
            Field = key;
            Value = value;
            Positions = positions;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}:{2}", Field, Value, Positions.Count);
        }
    }
}