using MathNet.Numerics.LinearAlgebra;
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

            return new IndexedVector(tuples, VectorWidth);
        }

        public AnalyzedData Tokenize(string text)
        {
            Span<char> source = text.ToCharArray();
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
                            embeddings.Add(new IndexedVector(embedding, source.Slice(offset, len).ToArray()));

                            embedding = new SortedList<int, float>();
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
                    embeddings.Add(new IndexedVector(embedding, source.Slice(offset, len).ToArray()));
                }
            }

            return new AnalyzedData(embeddings);
        }

        public double PrimaryIdenticalAngle => 0.4d;

        public double PrimaryFoldAngle => 0.9d;

        public double IdenticalAngle => 0.85d;

        public double FoldAngle => 0.45d;

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

            var otherVector = CreateVector.SparseOfIndexed(VectorWidth, tuples);

            var dotProduct = vector.Value.DotProduct(otherVector);
            var dotSelf1 = vector.Value.DotProduct(vector.Value);
            var dotSelf2 = otherVector.DotProduct(otherVector);

            return (dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }
    }
}
