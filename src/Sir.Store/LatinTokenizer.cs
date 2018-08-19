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
            var words = Normalize(text).Split(_wordDelimiters, StringSplitOptions.RemoveEmptyEntries);
            return words;

            //for (int i = 0; i < words.Length; i++)
            //{
            //    yield return words[i];

            //    var next = i + 1;

            //    if (next <= words.Length - 1)
            //    {
            //        yield return string.Join(' ', words[i], words[next]);
            //    }
            //}

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
