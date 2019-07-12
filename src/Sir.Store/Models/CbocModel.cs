using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Sir.Store
{
    public class CbocModel : IStringModel
    {
        public long SerializeVector(IVector vector, Stream vectorStream)
        {
            var pos = vectorStream.Position;

            vector.Serialize(vectorStream);

            return pos;
        }

        public IVector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream)
        {
            if (vectorStream == null)
            {
                throw new ArgumentNullException(nameof(vectorStream));
            }

            Span<byte> valuesBuf = new byte[componentCount * sizeof(float)];

            vectorStream.Seek(vectorOffset, SeekOrigin.Begin);
            vectorStream.Read(valuesBuf);

            Span<float> values = MemoryMarshal.Cast<byte, float>(valuesBuf);

            return new Vector(values.ToArray());
        }

        public AnalyzedData Tokenize(string text)
        {
            Memory<char> source = text.ToCharArray();
            var offset = 0;
            bool word = false;
            int index = 0;
            var embeddings = new List<IVector>();
            var embedding = new List<float>();

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
                            embedding = new List<float>();
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

        public double IdenticalAngle => 0.999999d;

        public double FoldAngle => 0.55d;

        public int PageWeight => 50000;

        public int VectorWidth => 100;

        public double CosAngle(IVector vec1, IVector vec2)
        {
            var dotProduct = vec1.Value.DotProduct(vec2.Value);
            var dotSelf1 = vec1.Value.DotProduct(vec1.Value);
            var dotSelf2 = vec2.Value.DotProduct(vec2.Value);

            return (dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }

        public double CosAngle(IVector vector, long vectorOffset, int componentCount, Stream vectorStream)
        {
            if (vectorStream == null)
            {
                throw new ArgumentNullException(nameof(vectorStream));
            }

            Span<byte> valuesBuf = new byte[componentCount * sizeof(float)];

            vectorStream.Seek(vectorOffset, SeekOrigin.Begin);
            vectorStream.Read(valuesBuf);

            Span<float> values = MemoryMarshal.Cast<byte, float>(valuesBuf);

            var otherVector = new Vector(values.ToArray());

            var dotProduct = vector.Value.DotProduct(otherVector.Value);
            var dotSelf1 = vector.Value.DotProduct(vector.Value);
            var dotSelf2 = otherVector.Value.DotProduct(otherVector.Value);

            return (dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }
    }
}
