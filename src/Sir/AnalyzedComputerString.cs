using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// An analyzed (tokenized) string.
    /// </summary>
    public class AnalyzedComputerString
    {
        public IList<Vector> Embeddings { get; private set; }

        public AnalyzedComputerString(IList<Vector> embeddings)
        {
            Embeddings = embeddings;
        }
    }
}