using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sir.Store
{
    public class LatinTokenizer : ITokenizer
    {
        private static char[] _wordDelimiters = new char[] {
                            ' '
                            };

        private static char[] _phraseDelimiters = new char[] {
                            '.', ',', '?', '!',
                            ':', ';', '\\', '/',
                            '\n', '\r', '\t',
                            '(', ')', '[', ']',
                            '"', '`', '´', '-'
                            };

        public string ContentType => "*";

        public IEnumerable<string> Tokenize(string text)
        {
            foreach (var phrase in Normalize(text).Split(_phraseDelimiters, StringSplitOptions.RemoveEmptyEntries))
            {
                //yield return phrase;

                foreach (var word in phrase.Split(_wordDelimiters, StringSplitOptions.RemoveEmptyEntries))
                {
                    yield return word;
                }
            }
        }

        public string Normalize(string text)
        {
            return text.ToLower(CultureInfo.CurrentCulture);
        }

        public void Dispose()
        {
        }
    }
}
