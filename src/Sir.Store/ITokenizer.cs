using System.Collections.Generic;

namespace Sir.Store
{
    /// <summary>
    /// Full-text tokenizer
    /// </summary>
    public interface ITokenizer
    {
        IEnumerable<string> Tokenize(string text);

        string Normalize(string text);
    }
}
