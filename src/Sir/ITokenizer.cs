using System.Collections.Generic;

namespace Sir
{
    /// <summary>
    /// Full-text tokenizer
    /// </summary>
    public interface ITokenizer : IPlugin
    {
        IEnumerable<string> Tokenize(string text);

        string Normalize(string text);
    }
}
