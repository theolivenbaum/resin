using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.Store
{
    public class BocModel : IStringModel
    {
        public long SerializeVector(IVector vector, Stream vectorStream)
        {
            var pos = vectorStream.Position;

            vector.Serialize(vectorStream);

            return pos;
        }

        public IVector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream)
        {
            if (vectorOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vectorOffset));
            }

            if (vectorStream == null)
            {
                throw new ArgumentNullException(nameof(vectorStream));
            }

            Span<byte> buf = new byte[componentCount * 2 * sizeof(float)];

            vectorStream.Seek(vectorOffset, SeekOrigin.Begin);
            vectorStream.Read(buf);

            var index = MemoryMarshal.Cast<byte, int>(buf.Slice(0, componentCount * sizeof(int)));
            var values = MemoryMarshal.Cast<byte, float>(buf.Slice(componentCount * sizeof(float)));
            var tuples = new Tuple<int, float>[componentCount];

            for (int i = 0; i < componentCount; i++)
            {
                tuples[i] = new Tuple<int, float>(index[i], values[i]);
            }

            return new IndexedVector(tuples);
        }

        public AnalyzedData Tokenize(string text)
        {
            var source = text.ToCharArray();
            var offset = 0;
            bool word = false;
            int index = 0;
            var embeddings = new List<IVector>();
            var embedding = new SortedList<int, float>();

            for (; index < source.Length; index++)
            {
                char c = char.ToLower(source[index]);

                if (word)
                {
                    if (!char.IsLetterOrDigit(c))
                    {
                        var len = index - offset;

                        if (len > 0)
                        {
                            embeddings.Add(new IndexedVector(embedding));

                            embedding = new SortedList<int, float>();
                        }

                        offset = index;
                        word = false;
                    }
                    else
                    {
                        embedding.AddOrPerformAddition(c, 1);
                    }
                }
                else
                {
                    if (char.IsLetterOrDigit(c))
                    {
                        word = true;
                        offset = index;

                        embedding.AddOrPerformAddition(c, 1);
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
                    embeddings.Add(new IndexedVector(embedding));
                }
            }

            return new AnalyzedData(embeddings);
        }

        public double IdenticalAngle => 0.999999d;

        public double FoldAngle => 0.55d;

        public double CosAngle(IVector vec1, IVector vec2)
        {
            var dotProduct = vec1.Value.DotProduct(vec2.Value);
            var dotSelf1 = vec1.Value.DotProduct(vec1.Value);
            var dotSelf2 = vec2.Value.DotProduct(vec2.Value);
            
            return (dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }
    }
}
