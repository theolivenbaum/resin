using System.Collections.Generic;

namespace Sir.Store
{
    public class UnicodeTokenizer : ITokenizer
    {
        public AnalyzedString Tokenize(string text)
        {
            var normalized = text.ToLower();
            var chars = normalized.ToCharArray();
            var offset = 0;
            bool word = false;
            int index = 0;
            var tokens = new List<(int, int)>();
            var embeddings = new List<SortedList<long, int>>();

            for (; index < chars.Length; index++)
            {
                char c = chars[index];

                if (word)
                {
                    if (!char.IsLetterOrDigit(c))
                    {
                        var len = index - offset;

                        if (len > 0)
                        {
                            tokens.Add((offset, index - offset));
                            embeddings.Add(normalized.ToVector(offset, len));

                        }

                        offset = index;

                        word = false;
                    }
                }
                else
                {
                    if (char.IsLetterOrDigit(c))
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
                {
                    tokens.Add((offset, index - offset));
                    embeddings.Add(normalized.ToVector(offset, len));

                }
            }

            return new AnalyzedString(tokens, embeddings, normalized);
        }
    }
}
