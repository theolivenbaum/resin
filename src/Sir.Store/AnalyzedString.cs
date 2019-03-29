using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// An analyzed (tokenized) string.
    /// </summary>
    public class AnalyzedString
    {
        public IList<(int offset, int length)> Tokens { get; set; }
        public char[] Source { get; set; }
        public string Original { get; set; }

        public IEnumerable<SortedList<long, byte>> Embeddings()
        {
            foreach (var token in Tokens)
            {
                yield return this.ToCharVector(token.offset, token.length);
            }
        }

        public override string ToString()
        {
            return Original;
        }
    }
}