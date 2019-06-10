using System;
using System.Collections.Generic;

namespace Sir.Store
{
    public class UnicodeTokenizer : ITokenizer
    {
        public AnalyzedString Tokenize(string text)
        {
            var source = text.AsMemory();
            var offset = 0;
            bool word = false;
            int index = 0;
            var embeddings = new List<Vector>();
            var embedding = new List<int>();

            for (; index < source.Length; index++)
            {
                char c = char.ToLower(source.Span[index]);

                if (word)
                {
                    if (!char.IsLetterOrDigit(c))
                    {
                        var len = index - offset;

                        if (len > 0)
                        {
                            embeddings.Add(new Vector(embedding.ToArray().AsMemory()));
                            embedding = new List<int>();
                        }

                        offset = index;
                        word = false;
                    }
                    else
                    {
                        embedding.Add(c);
                    }
                }
                else
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        word = true;
                        offset = index;
                        embedding.Add(c);
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
                    embeddings.Add(new Vector(embedding.ToArray().AsMemory()));
                }
            }

            return new AnalyzedString(embeddings);
        }
    }
}
