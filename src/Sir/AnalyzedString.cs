using System.Collections.Generic;

namespace Sir
{
    public class AnalyzedString
    {
        public IList<(int offset, int length)> Tokens { get; set; }
        public char[] Source { get; set; }
    }
}