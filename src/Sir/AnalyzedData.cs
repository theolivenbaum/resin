using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// An analyzed (tokenized) stream of computer words.
    /// </summary>
    public class AnalyzedData
    {
        public IList<Vector> Embeddings { get; private set; }

        public AnalyzedData(IList<Vector> embeddings)
        {
            Embeddings = embeddings;
        }
    }
}