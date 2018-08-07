using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sir.Store
{
    public class LatinTokenizer : ITokenizer
    {
        private static char[] _delimiters = new char[] {
                            '.', ',', '?', '!',
                            ':', ';', '\\', '/',
                            '\n', '\r', '\t',
                            '(', ')', '[', ']',
                            '"', '`', '´', '&'
                            };

        public string ContentType => "*";

        public IEnumerable<string> Tokenize(string text)
        {
            var phrases = Normalize(text)
                .Split(_delimiters, StringSplitOptions.RemoveEmptyEntries)
                .Where(x=>!string.IsNullOrWhiteSpace(x))
                .ToList();

            var words = phrases.SelectMany(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            foreach (var phrase in phrases)
            {
                yield return phrase;
            }

            foreach (var word in words)
            {
                yield return word;
            }
        }

        public void Dispose()
        {
        }

        public string Normalize(string text)
        {
            return text.ToLower(CultureInfo.CurrentCulture);
        }
    }
}
