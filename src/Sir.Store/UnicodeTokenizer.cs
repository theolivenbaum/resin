using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

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
            var tokens = new List<(int, int)>();
            var embeddings = new List<Vector>();
            var embedding = new SortedList<int, int>();

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
                            tokens.Add((offset, index - offset));
                            embeddings.Add(new Vector(embedding.Keys.ToArray().AsMemory(), embedding.Values.ToArray().AsMemory()));
                            embedding = new SortedList<int, int>();
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
                    embeddings.Add(new Vector(embedding.Keys.ToArray().AsMemory(), embedding.Values.ToArray().AsMemory()));
                }
            }

            return new AnalyzedString(tokens, embeddings, source);
        }
    }
}
