using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// An analyzed (tokenized) string.
    /// </summary>
    public class AnalyzedString
    {
        public IList<Vector> Embeddings { get; private set; }

        public AnalyzedString(IList<Vector> embeddings)
        {
            Embeddings = embeddings;
        }
    }
}