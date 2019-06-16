using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;

namespace Sir.Store
{
    public class BocModel : IStringModel
    {
        public long SerializeVector(Vector vector, Stream vectorStream)
        {
            Span<byte> index = MemoryMarshal.Cast<int, byte>(((IndexedVector)vector).Index.Span);
            Span<byte> values = MemoryMarshal.Cast<int, byte>(vector.Values.Span);

            var pos = vectorStream.Position;

            vectorStream.Write(index);
            vectorStream.Write(values);

            return pos;
        }

        public Vector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream)
        {
            if (vectorStream == null)
            {
                throw new ArgumentNullException(nameof(vectorStream));
            }

            Span<byte> indexBuf = new byte[componentCount * sizeof(int)];
            Span<byte> valuesBuf = new byte[componentCount * sizeof(int)];

            vectorStream.Seek(vectorOffset, SeekOrigin.Begin);
            vectorStream.Read(indexBuf);
            vectorStream.Read(valuesBuf);

            Span<int> index = MemoryMarshal.Cast<byte, int>(indexBuf);
            Span<int> values = MemoryMarshal.Cast<byte, int>(valuesBuf);

            return new IndexedVector(index.ToArray().AsMemory(), values.ToArray().AsMemory());
        }

        public Vector DeserializeVector(long vectorOffset, int componentCount, MemoryMappedViewAccessor vectorView)
        {
            if (vectorView == null)
            {
                throw new ArgumentNullException(nameof(vectorView));
            }

            var index = new int[componentCount];
            var values = new int[componentCount];

            vectorView.ReadArray(vectorOffset, index, 0, index.Length);
            vectorView.ReadArray(vectorOffset, values, 0, values.Length);

            return new IndexedVector(index, values);
        }

        public AnalyzedData Tokenize(string text)
        {
            var source = text.AsMemory();
            var offset = 0;
            bool word = false;
            int index = 0;
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
                            embeddings.Add(new IndexedVector
                                (embedding.Keys.ToArray().AsMemory(), 
                                embedding.Values.ToArray().AsMemory()));

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
                    embeddings.Add(new IndexedVector
                                (embedding.Keys.ToArray().AsMemory(),
                                embedding.Values.ToArray().AsMemory()));
                }
            }

            return new AnalyzedData(embeddings);
        }

        public (float identicalAngle, float foldAngle) Similarity()
        {
            return (0.999999f, 0.65f);
        }

        public float CosAngle(Vector vec1, Vector vec2)
        {
            long dotProduct = Dot((IndexedVector)vec1, (IndexedVector)vec2);
            long dotSelf1 = CbocModel.DotSelf(vec1);
            long dotSelf2 = CbocModel.DotSelf(vec2);

            return (float)(dotProduct / (Math.Sqrt(dotSelf1) * Math.Sqrt(dotSelf2)));
        }

        public static long Dot(IndexedVector vec1, IndexedVector vec2)
        {
            if (ReferenceEquals(vec1, vec2))
            {
                return CbocModel.DotSelf(vec1);
            }

            long product = 0;
            var cursor1 = 0;
            var cursor2 = 0;
            
            while (cursor1 < vec1.Count && cursor2 < vec2.Count)
            {
                var i1 = vec1.Index.Span[cursor1];
                var i2 = vec2.Index.Span[cursor2];

                if (i2 > i1)
                {
                    cursor1++;
                }
                else if (i1 > i2)
                {
                    cursor2++;
                }
                else
                {
                    product += vec1.Values.Span[cursor1++] * vec2.Values.Span[cursor2++];
                }
            }

            return product;
        }
    }
}
