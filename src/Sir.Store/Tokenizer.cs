using System;
using System.Globalization;

namespace Sir.Store
{
    public class Tokenizer : ITokenizer
    {
        private static char[] _delimiters = new char[] {
                            '.', ',', ';', ':', '?', '!',
                            '\n', '\r', '\t',
                            '(', ')', '[', ']',
                            '"', '`', '´'
                            };

        public string[] Tokenize(string text)
        {
            return text.ToLower(CultureInfo.CurrentCulture).Split(
                _delimiters, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
