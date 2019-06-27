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
        public Vector DeserializeVector(long vectorOffset, int componentCount, MemoryMappedViewAccessor vectorView)
        {
            if (vectorView == null)
            {
                throw new ArgumentNullException(nameof(vectorView));
            }

            var index = new int[componentCount];
            var values = new int[componentCount];

            var read = vectorView.ReadArray(vectorOffset, index, 0, index.Length);

            if (read < componentCount)
                throw new Exception("bad");

            read = vectorView.ReadArray(vectorOffset + componentCount * sizeof(int), values, 0, values.Length);

            if (read < componentCount)
                throw new Exception("bad");

            return new IndexedVector(index, values);
        }

        public long SerializeVector(Vector vector, Stream vectorStream)
        {
            Span<byte> index = MemoryMarshal.Cast<int, byte>(((IndexedVector)vector).Index);
            Span<byte> values = MemoryMarshal.Cast<int, byte>(vector.Values);

            var pos = vectorStream.Position;

            vectorStream.Write(index);
            vectorStream.Write(values);

            return pos;
        }

        public Vector DeserializeVector(long vectorOffset, int componentCount, Stream vectorStream)
        {
            if (vectorOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vectorOffset));
            }

            if (vectorStream == null)
            {
                throw new ArgumentNullException(nameof(vectorStream));
            }

            Span<byte> buf = new byte[componentCount * 2 * sizeof(int)];

            vectorStream.Seek(vectorOffset, SeekOrigin.Begin);
            vectorStream.Read(buf);

            Span<int> all = MemoryMarshal.Cast<byte, int>(buf);
            int[] index = all.Slice(0, componentCount).ToArray();
            int[] values = all.Slice(componentCount, componentCount).ToArray();

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
                                (embedding.Keys.ToArray(), 
                                embedding.Values.ToArray()));

                            embedding = new SortedList<int, int>();
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
                    embeddings.Add(new IndexedVector
                                (embedding.Keys.ToArray(),
                                embedding.Values.ToArray()));
                }
            }

            return new AnalyzedData(embeddings);
        }

        public float IdenticalAngle => 0.999999f;

        public float FoldAngle => 0.55f;

        public float CosAngle(Vector vec1, Vector vec2)
        {
            var ivec1 = vec1 as IndexedVector;
            var ivec2 = vec2 as IndexedVector;

            if (ivec1 == null || ivec2 == null)
            {
                return 0;
            }

            long dotProduct = Dot(ivec1, ivec2);
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
                var i1 = vec1.Index[cursor1];
                var i2 = vec2.Index[cursor2];

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
                    product += vec1.Values[cursor1++] * vec2.Values[cursor2++];
                }
            }

            return product;
        }
    }
}
