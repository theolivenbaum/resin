using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sir.Store
{
    public class LatinTokenizer : ITokenizer
    {
        private static char[] _wordDelimiters = new char[] {
                            '.', ',', '?', '!',
                            ':', ';', '\\', '/',
                            '\n', '\r', '\t',
                            '(', ')', '[', ']',
                            '"', '`', '´', '-',
                            ' '
                            };

        public string ContentType => "*";

        public IEnumerable<string> Tokenize(string text)
        {
            return Normalize(text).Split(_wordDelimiters, StringSplitOptions.RemoveEmptyEntries);
            //var phrase = new List<string>();

            //foreach (var word in Normalize(text).Split(_wordDelimiters, StringSplitOptions.RemoveEmptyEntries))
            //{
            //    phrase.Add(word);

            //    if (phrase.Count == 2)
            //    {
            //        yield return string.Join(" ", phrase);

            //        phrase.Clear();
            //    }
            //}

            //if (phrase.Count > 0)
            //{
            //    yield return string.Join(" ", phrase);
            //}
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
