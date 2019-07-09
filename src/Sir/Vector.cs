using MathNet.Numerics.LinearAlgebra.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Sir
{
    public interface IVector
    {
        MathNet.Numerics.LinearAlgebra.Vector<float> Value { get; }
        void Serialize(Stream stream);
        int ComponentCount { get; }
    }

    public class Vector : IVector
    {
        public MathNet.Numerics.LinearAlgebra.Vector<float> Value { get; private set; }
        public int ComponentCount { get; }

        public Vector(MathNet.Numerics.LinearAlgebra.Vector<float> vector)
        {
            Value = vector;
            ComponentCount = ((DenseVectorStorage<float>)vector.Storage).Length;
        }

        public Vector(IList<float> vector)
        {
            Value = MathNet.Numerics.LinearAlgebra.CreateVector.Dense(vector.ToArray());
            ComponentCount = ((DenseVectorStorage<float>)Value.Storage).Length;
        }

        public void Serialize(Stream stream)
        {
            stream.Write(MemoryMarshal.Cast<float, byte>(Value.Storage.AsArray()));
        }
    }

    public class IndexedVector : IVector
    {
        public MathNet.Numerics.LinearAlgebra.Vector<float> Value { get; private set; }
        public int ComponentCount { get; }

        public IndexedVector(MathNet.Numerics.LinearAlgebra.Vector<float> value)
        {
            Value = value;
            ComponentCount = ((SparseVectorStorage<float>)Value.Storage).ValueCount;
        }

        public IndexedVector(SortedList<int, float> dictionary)
        {
            var tuples = new Tuple<int, float>[dictionary.Count];

            var i = 0;

            foreach (var p in dictionary)
            {
                tuples[i++] = new Tuple<int, float>(p.Key, p.Value);
            }

            Value = MathNet.Numerics.LinearAlgebra.CreateVector.SparseOfIndexed(1000, tuples);
            ComponentCount = tuples.Length;
        }

        public void Serialize(Stream stream)
        {
            stream.Write(MemoryMarshal.Cast<int, byte>(((SparseVectorStorage<float>)Value.Storage).Indices));
            stream.Write(MemoryMarshal.Cast<float, byte>(((SparseVectorStorage<float>)Value.Storage).Values));
        }
    }
}