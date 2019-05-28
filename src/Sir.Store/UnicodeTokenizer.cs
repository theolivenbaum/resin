using System.Collections.Generic;

namespace Sir.Store
{
    public class UnicodeTokenizer : ITokenizer
    {
        public AnalyzedString Tokenize(string text)
        {
            var offset = 0;
            bool word = false;
            int index = 0;
            var tokens = new List<(int, int)>();
            var embeddings = new List<SortedList<long, int>>();
            var embedding = new SortedList<long, int>();

            for (; index < text.Length; index++)
            {
                char c = char.ToLower(text[index]);

                if (word)
                {
                    if (!char.IsLetterOrDigit(c))
                    {
                        var len = index - offset;

                        if (len > 0)
                        {
                            tokens.Add((offset, index - offset));
                            embeddings.Add(embedding);
                            embedding = new SortedList<long, int>();
                        }

                        offset = index;
                        word = false;
                    }
                    else
                    {
                        embedding.AddOrAppendToComponent(c, 1);
                    }
                }
                else
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        word = true;
                        offset = index;
                        embedding.AddOrAppendToComponent(c, 1);
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
                    embeddings.Add(embedding);

                }
            }

            return new AnalyzedString(tokens, embeddings, text);
        }
    }
}
