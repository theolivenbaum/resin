namespace Sir.Store
{
    /// <summary>
    /// Latin text tokenizer.
    /// </summary>
    public interface ITokenizer
    {
        AnalyzedString Tokenize(string text);
    }
}
