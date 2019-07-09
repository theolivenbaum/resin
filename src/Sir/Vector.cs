using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Sir
{
    public class Vector : IVector
    {
        public Vector<float> Value { get; private set; }
        public int ComponentCount { get; }

        public Vector(IList<float> vector)
        {
            Value = CreateVector.Dense(vector.ToArray());
            ComponentCount = ((DenseVectorStorage<float>)Value.Storage).Length;
        }

        public void Serialize(Stream stream)
        {
            stream.Write(MemoryMarshal.Cast<float, byte>(Value.Storage.AsArray()));
        }
    }

    public class IndexedVector : IVector
    {
        public Vector<float> Value { get; private set; }
        public int ComponentCount { get; }

        public IndexedVector(SortedList<int, float> dictionary, int vectorWidth = 100)
        {
            var tuples = new Tuple<int, float>[dictionary.Count];

            var i = 0;

            foreach (var p in dictionary)
            {
                tuples[i++] = new Tuple<int, float>(p.Key, p.Value);
            }

            Value = CreateVector.Sparse(
                SparseVectorStorage<float>.OfIndexedEnumerable(vectorWidth, tuples));

            ComponentCount = tuples.Length;
        }

        public IndexedVector(Tuple<int, float>[] tuples, int vectorWidth = 100)
        {
            Value = CreateVector.SparseOfIndexed(vectorWidth, tuples);
            ComponentCount = tuples.Length;
        }

        public void Serialize(Stream stream)
        {
            stream.Write(MemoryMarshal.Cast<int, byte>(((SparseVectorStorage<float>)Value.Storage).Indices));
            stream.Write(MemoryMarshal.Cast<float, byte>(((SparseVectorStorage<float>)Value.Storage).Values));
        }
    }

    public interface IVector
    {
        Vector<float> Value { get; }
        void Serialize(Stream stream);
        int ComponentCount { get; }
    }
}