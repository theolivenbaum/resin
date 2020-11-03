using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Sir.VectorSpace
{
    public static class VectorOperations
    {
        public static void AddOrAppendToComponent(this SortedList<int, float> vec, int key)
        {
            float v;

            if (vec.TryGetValue(key, out v))
            {
                vec[key] = v + 1;
            }
            else
            {
                vec.Add(key, 1);
            }
        }

        public static IVector DeserializeVector(
            long vectorOffset, int componentCount, int vectorWidth, MemoryMappedViewAccessor vectorView)
        {
            if (vectorView == null)
            {
                throw new ArgumentNullException(nameof(vectorView));
            }

            var index = new int[componentCount];
            var values = new float[componentCount];

            var read = vectorView.ReadArray(vectorOffset, index, 0, index.Length);

            if (read < componentCount)
                throw new Exception("bad");

            read = vectorView.ReadArray(vectorOffset + (componentCount * sizeof(int)), values, 0, values.Length);

            if (read < componentCount)
                throw new Exception("bad");

            return new IndexedVector(index, values, vectorWidth, null);
        }

        public static IVector DeserializeVector(long vectorOffset, int componentCount, int vectorWidth, Stream vectorStream)
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

            return new IndexedVector(tuples, vectorWidth);
        }

        public static long SerializeVector(IVector vector, Stream vectorStream)
        {
            var pos = vectorStream.Position;

            vector.Serialize(vectorStream);

            return pos;
        }
    }
}
