using System.Collections.Generic;

namespace Sir
{
    public class AnalyzedString
    {
        public IList<(int offset, int length)> Tokens { get; set; }
        public char[] Source { get; set; }
        public string Original { get; set; }
        public IEnumerable<SortedList<int, byte>> Embeddings
        {
            get
            {
                foreach (var token in Tokens)
                {
                    yield return this.ToCharVector(token.offset, token.length);
                }
            }
        }
    }
}