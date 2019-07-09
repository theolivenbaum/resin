using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// An analyzed (tokenized) stream of computer words.
    /// </summary>
    public class AnalyzedData
    {
        public IList<IVector> Embeddings { get; private set; }

        public AnalyzedData(IList<IVector> embeddings)
        {
            Embeddings = embeddings;
        }
    }
}