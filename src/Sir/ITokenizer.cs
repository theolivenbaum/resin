namespace Sir
{
    /// <summary>
    /// Full-text tokenizer
    /// </summary>
    public interface ITokenizer : IPlugin
    {
        string[] Tokenize(string text);
    }
}
