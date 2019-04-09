using System.Collections.Generic;

namespace Sir.Store
{
    public class LatinTokenizer : ITokenizer
    {
        private static char[] _delims = new char[] {
                            '.', ',', '?', '!',
                            ':', ';', '\\', '/',
                            '\n', '\r', '\t',
                            '(', ')', '[', ']',
                            '"', '`', '´', '-',
                            '=', '&', '\'', '+', ' '
                            };

        public string ContentType => "*";

        public AnalyzedString Tokenize(string text)
        {
            var normalized = text.ToLower().ToCharArray();
            var offset = 0;
            bool word = false;
            int index = 0;
            var tokens = new List<(int, int)>();

            for (; index < normalized.Length; index++)
            {
                char c = normalized[index];

                if (word)
                {
                    if (!IsData(c))
                    {
                        var len = index - offset;

                        if (len > 0)
                            tokens.Add((offset, index - offset));

                        offset = index;

                        word = false;
                    }
                }
                else
                {
                    if (IsData(c))
                    {
                        word = true;
                        offset = index;
                    }
                    else
                    {
                        offset++;
                    }
                }
            }

            if (word)
            {
                var len = index - offset;

                if (len > 0)
                    tokens.Add((offset, index - offset));
            }

            return new AnalyzedString { Source = normalized, Tokens = tokens, Original = text };
        }

        private bool IsData(char c)
        {
            return char.IsLetterOrDigit(c) && !IsDelimiter(c);
        }

        private static bool IsDelimiter(char c)
        {
            foreach (var d in _delims)
            {
                if (c == d) return true;
            }

            return false;
        }

        public void Dispose()
        {
        }
    }
}
