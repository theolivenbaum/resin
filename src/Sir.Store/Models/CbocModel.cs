using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.Store
{
    public class CbocModel : IStringModel
    {
        public Vector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream)
        {
            if (vectorStream == null)
            {
                throw new ArgumentNullException(nameof(vectorStream));
            }

            Span<byte> valuesBuf = new byte[componentCount * sizeof(int)];

            vectorStream.Seek(vectorOffset, SeekOrigin.Begin);
            vectorStream.Read(valuesBuf);

            Span<int> values = MemoryMarshal.Cast<byte, int>(valuesBuf);

            return new Vector(values.ToArray());
        }

        public long SerializeVector(Vector vector, Stream vectorStream)
        {
            Span<byte> values = MemoryMarshal.Cast<int, byte>(vector.Values);

            var pos = vectorStream.Position;

            vectorStream.Write(values);

            return pos;
        }

        public AnalyzedData Tokenize(string text)
        {
            Memory<char> source = text.ToCharArray();
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
                            embeddings.Add(new Vector(embedding.ToArray()));
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
                    embeddings.Add(new Vector(embedding.ToArray()));
                }
            }

            return new AnalyzedData(embeddings);
        }

        public float IdenticalAngle => 0.999999f;

        public float FoldAngle => 0.55f;

        public float CosAngle(Vector vec1, Vector vec2)
        {
            long dotProduct = Dot(vec1, vec2);
            long dotSelf1 = DotSelf(vec1);
            long dotSelf2 = DotSelf(vec2);

            return (float)(dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }

        private static long Dot(Vector vec1, Vector vec2)
        {
            if (ReferenceEquals(vec1, vec2))
                return DotSelf(vec1);

            long product = 0;
            var shorter = vec1.Count < vec2.Count ? vec1 : vec2;
            var longer = ReferenceEquals(vec1, shorter) ? vec2 : vec1;
            int dimension = 0;

            for (; dimension < shorter.Count; dimension++)
            {
                product += shorter.Values[dimension] * longer.Values[dimension];
            }

            return product;
        }

        public static long DotSelf(Vector vec)
        {
            long product = 0;

            foreach (var component in vec.Values)
            {
                product += (component * component);
            }

            return product;
        }
    }
}
