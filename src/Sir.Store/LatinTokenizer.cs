using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sir.Store
{
    public class LatinTokenizer : ITokenizer
    {
        private static char[] _phraseDelimiters = new char[] {
                            '.', ',', '?', '!',
                            ':', ';', '\\', '/',
                            '\n', '\r', '\t',
                            '(', ')', '[', ']',
                            '"', '`', '´', '-',
                            '=', '&', '\''
                            };

        private static char[] _delims = new char[] {
                            '.', ',', '?', '!',
                            ':', ';', '\\', '/',
                            '\n', '\r', '\t',
                            '(', ')', '[', ']',
                            '"', '`', '´', '-',
                            '=', '&', '\'', ' '
                            };

        public string ContentType => "*";


        private IEnumerable<string> TokenizeIntoBigrams(string text)
        {
            string item1 = null;
            string item2 = null;

            foreach (var word in Normalize(text).Split(_delims, StringSplitOptions.None)
                .Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (item1 != null && item2 != null)
                {
                    yield return string.Join(' ', item1, item2);
                    item1 = item2;
                    item2 = word;
                }
                else if (item1 == null)
                {
                    item1 = word;
                }
                else if (item2 == null)
                {
                    item2 = word;
                }
            }

            if (item1 != null && item2 != null)
            {
                yield return string.Join(' ', item1, item2);
            }
            else if (item1 != null)
            {
                yield return item1;
            }
        }

        public IEnumerable<string> Tokenize(string text)
        {
            return Normalize(text).Split(_delims, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x));
        }

        public string Normalize(string text)
        {
            return text.ToLower(CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
        }
    }

    public static class StringExtensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(
        this IEnumerable<T> source, int size)
        {
            T[] bucket = null;
            var count = 0;

            foreach (var item in source)
            {
                if (bucket == null)
                    bucket = new T[size];

                bucket[count++] = item;

                if (count != size)
                    continue;

                yield return bucket;

                bucket = null;
                count = 0;
            }

            // Return the last bucket with all remaining elements
            if (bucket != null && count > 0)
                yield return bucket.Take(count);
        }
    }
}
